using DualSenseBridge.Core;
using HIDMaestro;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DualSenseBridge.HidMaestro;

public sealed class HidMaestroVirtualGamepadOutput : IVirtualGamepadOutput
{
    private const string DriverCertificateSubject = "CN=HIDMaestroTestCert";
    private const string DriverCertificateName = "HIDMaestroTestCert";

    private HMContext? _context;
    private HMController? _controller;

    public bool IsConnected => _controller is not null;

    public static void InstallDriver()
    {
        EnsureDriverCertificate();
        using var context = new HMContext();
        context.LoadDefaultProfiles();
        context.InstallDriver();
    }

    public static void UninstallDriver() => HMContext.RemoveAllVirtualControllers();

    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsConnected)
        {
            return ValueTask.CompletedTask;
        }

        var context = new HMContext();
        try
        {
            context.LoadDefaultProfiles();
            var profile = context.GetProfile("xbox-360-wired")
                ?? throw new InvalidOperationException("HIDMaestro no contiene el perfil xbox-360-wired.");
            var controller = context.CreateController(profile);
            _context = context;
            _controller = controller;
            return ValueTask.CompletedTask;
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }

    public ValueTask SubmitAsync(XboxControllerState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var controller = _controller
            ?? throw new InvalidOperationException("El control virtual todavía no está conectado.");
        var hidState = new HMGamepadState
        {
            Axes = HMGamepadStateHelpers.StandardAxes(
                controller.Profile,
                state.LeftStick.X,
                state.LeftStick.Y,
                state.RightStick.X,
                state.RightStick.Y,
                state.Triggers.Left,
                state.Triggers.Right),
            Buttons = MapButtons(state.Buttons),
            Hat = MapHat(state.DPad),
        };

        controller.SubmitState(in hidState);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _controller?.Dispose();
        _context?.Dispose();
        _controller = null;
        _context = null;
        return ValueTask.CompletedTask;
    }

    private static HMButton MapButtons(XboxButtons source)
    {
        var result = HMButton.None;
        Add(source, XboxButtons.A, HMButton.A, ref result);
        Add(source, XboxButtons.B, HMButton.B, ref result);
        Add(source, XboxButtons.X, HMButton.X, ref result);
        Add(source, XboxButtons.Y, HMButton.Y, ref result);
        Add(source, XboxButtons.LeftBumper, HMButton.LeftBumper, ref result);
        Add(source, XboxButtons.RightBumper, HMButton.RightBumper, ref result);
        Add(source, XboxButtons.Back, HMButton.Back, ref result);
        Add(source, XboxButtons.Start, HMButton.Start, ref result);
        Add(source, XboxButtons.LeftStick, HMButton.LeftStick, ref result);
        Add(source, XboxButtons.RightStick, HMButton.RightStick, ref result);
        Add(source, XboxButtons.Guide, HMButton.Guide, ref result);
        return result;
    }

    private static HMHat MapHat(XboxDPad direction) => direction switch
    {
        XboxDPad.Up => HMHat.North,
        XboxDPad.UpRight => HMHat.NorthEast,
        XboxDPad.Right => HMHat.East,
        XboxDPad.DownRight => HMHat.SouthEast,
        XboxDPad.Down => HMHat.South,
        XboxDPad.DownLeft => HMHat.SouthWest,
        XboxDPad.Left => HMHat.West,
        XboxDPad.UpLeft => HMHat.NorthWest,
        _ => HMHat.None,
    };

    private static void Add(XboxButtons source, XboxButtons expected, HMButton target, ref HMButton result)
    {
        if (source.HasFlag(expected))
        {
            result |= target;
        }
    }

    private static void EnsureDriverCertificate()
    {
        using (var machineStore = new X509Store(StoreName.My, StoreLocation.LocalMachine))
        {
            machineStore.Open(OpenFlags.ReadOnly);
            var existing = machineStore.Certificates.Find(
                X509FindType.FindBySubjectDistinguishedName,
                DriverCertificateSubject,
                validOnly: false);

            if (existing.OfType<X509Certificate2>().Any(certificate => certificate.HasPrivateKey))
            {
                return;
            }
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            DriverCertificateSubject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature,
            critical: false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.3") },
            critical: false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(
            request.PublicKey,
            critical: false));

        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10));

        // HIDMaestro 1.7.3 uses an empty PFX password here. Some current
        // Windows/.NET combinations reject that PFX as an invalid password.
        // A random, one-use password produces the same persisted certificate.
        var temporaryPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var pfx = generated.Export(X509ContentType.Pfx, temporaryPassword);
        using var certificate = X509CertificateLoader.LoadPkcs12(
            pfx,
            temporaryPassword,
            X509KeyStorageFlags.PersistKeySet |
            X509KeyStorageFlags.MachineKeySet |
            X509KeyStorageFlags.Exportable);
        CryptographicOperations.ZeroMemory(pfx);
        certificate.FriendlyName = DriverCertificateName;

        AddCertificate(certificate, StoreName.My);
        AddCertificate(certificate, StoreName.Root);
        AddCertificate(certificate, StoreName.TrustedPublisher);
    }

    private static void AddCertificate(X509Certificate2 certificate, StoreName storeName)
    {
        using var store = new X509Store(storeName, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        store.Add(certificate);
    }
}
