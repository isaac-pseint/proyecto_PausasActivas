using System;
using System.Collections.Generic;
using System.Windows.Forms;
using PausasEmpleado.Forms;
using PausasEmpleado.Helpers;

namespace PausasEmpleado.Managers;

/// <summary>
/// Coordina la visualización simultánea de la pausa activa en todas las pantallas (Screen.AllScreens),
/// la sincronización del contador regresivo y la gestión de desbloqueo de emergencia.
/// </summary>
public class PauseManager : IDisposable
{
    private readonly string _pinEmergencia;
    private readonly List<VentanaPausaForm> _ventanasAbiertas = new();
    private readonly System.Windows.Forms.Timer _timerCuentaRegresiva;
    private int _segundosRestantes;
    private int _duracionTotalSeg;
    private bool _dialogoEmergenciaAbierto;
    private bool _disposed;

    public bool HayPausaActiva => _ventanasAbiertas.Count > 0;

    public event Action? PausaIniciada;
    public event Action<bool>? PausaFinalizada; // true = completada normal, false = cancelada por PIN

    public PauseManager(string? pinEmergencia)
    {
        _pinEmergencia = string.IsNullOrEmpty(pinEmergencia) ? "1234" : pinEmergencia;

        _timerCuentaRegresiva = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _timerCuentaRegresiva.Tick += TimerCuentaRegresiva_Tick;
    }

    /// <summary>
    /// Inicia la pausa activa mostrando una ventana a pantalla completa en cada monitor disponible.
    /// </summary>
    public void IniciarPausa(byte[]? imagenBytes, string nombreImagen, string? categoriaImagen, int duracionSeg)
    {
        if (HayPausaActiva)
        {
            Logger.Advertencia("Intento de iniciar pausa cuando ya hay una activa.");
            return;
        }

        _duracionTotalSeg = duracionSeg > 0 ? duracionSeg : 120;
        _segundosRestantes = _duracionTotalSeg;

        Logger.Info($"Iniciando pausa activa ({_duracionTotalSeg} seg) en {Screen.AllScreens.Length} pantalla(s)...");

        // Crear una ventana por cada monitor detectado
        foreach (var screen in Screen.AllScreens)
        {
            var ventana = new VentanaPausaForm(
                screen,
                imagenBytes,
                nombreImagen,
                categoriaImagen,
                _duracionTotalSeg,
                OnEmergenciaSolicitada);

            _ventanasAbiertas.Add(ventana);
            ventana.Show();
        }

        _timerCuentaRegresiva.Start();
        PausaIniciada?.Invoke();
    }

    private void TimerCuentaRegresiva_Tick(object? sender, EventArgs e)
    {
        _segundosRestantes--;

        foreach (var ventana in _ventanasAbiertas)
        {
            ventana.ActualizarTiempo(_segundosRestantes);
        }

        if (_segundosRestantes <= 0)
        {
            _timerCuentaRegresiva.Stop();
            Logger.Info("Pausa activa finalizada normalmente por tiempo cumplido.");
            CerrarTodasLasVentanas(motivoNormal: true);
        }
    }

    private void OnEmergenciaSolicitada()
    {
        if (_dialogoEmergenciaAbierto) return;

        try
        {
            _dialogoEmergenciaAbierto = true;
            using var dlg = new PinEmergenciaForm(_pinEmergencia);
            var resultado = dlg.ShowDialog();

            if (resultado == DialogResult.OK && dlg.DesbloqueoExitoso)
            {
                Logger.Info("Pausa activa cancelada mediante PIN de emergencia.");
                _timerCuentaRegresiva.Stop();
                CerrarTodasLasVentanas(motivoNormal: false);
            }
        }
        finally
        {
            _dialogoEmergenciaAbierto = false;
        }
    }

    private void CerrarTodasLasVentanas(bool motivoNormal)
    {
        var copiaVentanas = new List<VentanaPausaForm>(_ventanasAbiertas);
        _ventanasAbiertas.Clear();

        foreach (var ventana in copiaVentanas)
        {
            try
            {
                ventana.PermitirCierreYCerrar();
                ventana.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error("Error al cerrar ventana de pausa", ex);
            }
        }

        PausaFinalizada?.Invoke(motivoNormal);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timerCuentaRegresiva.Stop();
        _timerCuentaRegresiva.Dispose();
        CerrarTodasLasVentanas(motivoNormal: false);
        GC.SuppressFinalize(this);
    }
}
