# BedrockLoader

Minecraft Bedrock için Windows üzerinde çalışan mod dosyası ve eklenti yöneticisi. C# / Windows Forms / .NET 8.

## İndir ve çalıştır

[BedrockLoader v0.12 — Windows ZIP paketini indir](https://github.com/kokluk/Bedrockloader/raw/refs/heads/main/downloads/BedrockLoader-v0.12-win.zip)

1. ZIP dosyasını bir klasöre çıkar.
2. `BedrockLoader.exe` dosyasını aç. Yanındaki DLL ve JSON dosyalarını aynı klasörde tut.
3. Bilgisayarında **.NET 8 Windows Desktop Runtime** kurulu olmalı.

## Özellikler

- Ana ekrandan Minecraft başlatma ve mod/eklenti kütüphanesine erişim.
- Yerel mod dosyalarını ekleme, listeleme ve seçilenleri onayla silme.
- `.mcpack`, `.mcaddon` ve uyumlu `.zip` eklenti paketlerini kurma.
- Türkçe / İngilizce arayüz, açık / koyu görünüm ve tema renkleri.
- Animasyonlu düğmeler, tema ile uyumlu sol menü ve boyutlandırılabilir pencere.
- İşlem kayıtları ve oyun Content klasörü ayarı.

## İzinli modloader kurulumu

Ana ekrandaki **Modloader'ı kur** düğmesi kaynak bağlantısını ve hedef dosyayı göstererek kullanıcıdan izin ister. Onay verilmeden indirme veya dosya yazma başlamaz. Hedefte aynı dosya varsa değiştirmek için ayrıca onay alınır.

Kullanılan bağlantı:

`https://github.com/kokluk/BedrockFree/releases/download/McBedrock/vcruntime140_1.dll`

Dosya, ayarlarda seçilen Minecraft **Content** klasörüne kurulur. Klasör yoksa kullanıcıya seçtirilir. İndirme geçici dosyada tamamlanır, Windows DLL biçimi kontrol edilir ve sonra hedefe taşınır. İndirme sınırı 64 MB'dır. Bu DLL dağıtım ZIP'ine dahil değildir; yalnızca kullanıcı izin verirse ayrı olarak indirilir.

Yerel mod dosyası yönetimi ile modloader kurulumu ayrı işlemlerdir. DLL'in Minecraft sürümüyle uyumluluğu harici modloader kaynağına bağlıdır.

## Kaynaktan derleme

Gereksinimler: Windows 10/11 ve .NET 8 SDK. Visual Studio kullanılıyorsa .NET masaüstü geliştirme iş yükünü kur.

```powershell
dotnet build KoklukBedrockModloader.csproj -c Release -o artifacts
```

Derlenen uygulama: `artifacts/BedrockLoader.exe`.

## Proje dosyaları

- `MainForm.cs`: pencere, menü, ayarlar, mod ve eklenti işlemleri.
- `ComfortablePages.cs`: ana ekran, eklentiler ve hakkında sayfaları.
- `AnimatedRoundButton.cs`: düğme animasyonları.
- `ModloaderInstaller.cs`: izinli indirme ve Content klasörüne kurulum.

Ayarlar ve işlem kayıtları `%APPDATA%\BedrockLoader` altında tutulur. Yerel mod klasörü `%APPDATA%\Minecraft bedrock\mods` konumundadır.

## Lisans

[MIT](LICENSE). Harici modloader bu deponun kaynak koduna ve dağıtımına dahil değildir.
