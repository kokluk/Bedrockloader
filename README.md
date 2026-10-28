# BedrockLoader

Minecraft Bedrock için Windows mod ve paket yöneticisi. Güncel sürüm: **v0.15.10**.

- Mod dosyalarını ekleme, sürükle-bırak ve onayla silme.
- Kaynak ve davranış paketlerini ayrı sütunlarda yönetme; `.mcpack`, `.mcaddon` ve uyumlu ZIP yükleme.
- Tema ve dil seçimi, yumuşak düğme animasyonları ve aynı düğmeyle açılıp kapanan menüler.
- Yeni sürüm bildirimi; indirme ve kurulum kullanıcı onayına bağlıdır.
- Türkçe/İngilizce, Windows temasını izleyen açık/koyu gri kurulum sihirbazı.

## Windows kurulumu

Kurulum dosyası: `BedrockLoader-Setup-v0.15.10-x64.exe`. Windows 10/11 x64 için hazırlanır; .NET çalışma zamanı dahildir. Kurulum kullanıcı hesabına yapılır ve yönetici izni gerektirmez.

[Yayınlanan sürümler](https://github.com/kokluk/Bedrockloader/releases)

## Kaynaktan derleme

Windows ve .NET 8 SDK gerekir:

```powershell
dotnet build .\KoklukBedrockModloader.csproj -c Release
```

Inno Setup 6.7.3 ile kurulum paketi üretmek için:

```powershell
.\installer\build.ps1 -IsccPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

Kurulum çıktısı `installer-output/`, bağımsız uygulama dosyaları `installer-payload-v0.15.10/` altında oluşur.

## Kontroller

```powershell
dotnet run --project .\tests\PackLibrary.Tests -c Release
dotnet run --project .\tests\ReleaseUpdates.Tests -c Release -- .\installer-payload-v0.15.10\BedrockLoader.exe
```

İkinci kontrol için önce kurulum derleme betiğini çalıştır. Kontroller yükleyiciyi çalıştırmaz ve kullanıcının paketlerini değiştirmez.

## Modloader ve veriler

Ana ekrandaki **Modloader'ı kur**, kaynak URL'yi ve Content hedefini göstererek izin ister. Mevcut DLL değiştirilecekse ayrıca onay alınır. Harici DLL kaynak koduna veya kurulum paketine dahil değildir; uyumluluğu harici sağlayıcıya bağlıdır.

Kaynak: https://github.com/kokluk/BedrockFree/releases/download/McBedrock/vcruntime140_1.dll

Ayarlar/kayıtlar `%APPDATA%\BedrockLoader`, yerel modlar `%APPDATA%\Minecraft bedrock\mods` altında tutulur. Kaldırma, Minecraft paketlerini ve kullanıcı ayarlarını silmez.

Dahili mod deposu şimdilik **Yakında** ekranıdır. Güncelleyicinin dağıtım için erişilebilir bir GitHub Release ve doğrulanabilir kurulum dosyası bulunması gerekir. Depo özel ise anonim erişimle güncelleme alınamaz.

MIT lisansı — Kokluk. Eski v0.12 Windows ZIP'i `downloads/` altında korunur.
