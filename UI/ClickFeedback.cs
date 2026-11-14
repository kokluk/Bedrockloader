using System.Media;
using System.Runtime.InteropServices;
using System.Text;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static readonly Lazy<byte[]> ClickWave = new(CreateClickWave);

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool PlayWave(nint wave, nint module, uint flags);

    static void PlayClick(Control control)
    {
        var window = control.FindForm();
        var launcher = window as MainForm ?? window?.Owner as MainForm;
        if (launcher?.settings.ClickSounds != true) return;
        PlayClickSample();
    }

    static void PlayClickSample()
    {
        try
        {
            if (!PlayWave(Marshal.UnsafeAddrOfPinnedArrayElement(ClickWave.Value, 0), 0, 0x0007))
                SystemSounds.Beep.Play();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception) { }
    }

    static byte[] CreateClickWave()
    {
        const int sampleRate = 22050, samples = 1200;
        using var wave = new MemoryStream();
        using (var writer = new BinaryWriter(wave, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
            writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(samples * 2);
            for (int i = 0; i < samples; i++)
                writer.Write((short)(9500 * (.65 * Math.Sin(i * .28) + .35 * Math.Sin(i * .45)) *
                    Math.Exp(-i / 250d) * Math.Min(1, i / 12d)));
        }
        var pinned = GC.AllocateUninitializedArray<byte>((int)wave.Length, pinned: true);
        wave.GetBuffer().AsSpan(0, pinned.Length).CopyTo(pinned);
        return pinned;
    }
}
