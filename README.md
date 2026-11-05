# BedrockLoader

Minecraft Bedrock ve Cinnabar için Windows başlatıcısı ve içerik yöneticisi. Kaynak sürümü **v0.16.10**.

## Cinnabar modu

Ana ekrandaki değiştirme düğmesiyle Minecraft ve Cinnabar arasında geçebilirsin. Cinnabar, Rust ile geliştirilen bağımsız bir Bedrock istemcisidir. BedrockLoader, Cinnabar’ın resmî Windows paketini indirip doğrulayabilir, kurulu sürümü başlatabilir ve güncellemeleri denetleyebilir.

Cinnabar modunda yerel dünyaları ve shader paketlerini görüntüleyebilir, CurseForge’dan uyumlu Java shader paketlerini indirebilir ve seçili shader’ları Geri Dönüşüm Kutusu’na taşıyabilirsin. Launcher’da kayıtlı sunucular bu modda da görünür. Adresi kopyalayıp Cinnabar’ın kendi sunucu ekranına yapıştırabilirsin. [Cinnabar projesi](https://github.com/cinnabar-mc/cinnabar)

## Öne çıkan özellikler

- **Sunucular** — IPv4, IPv6 ve alan adı desteği, favoriler ve arka planda gecikme sorgusu. Minecraft’a sunucu gönderme ve oyun kapalıyken üçüncü taraf IPv4 veya alan adı kayıtlarını yedek alarak düzenleme.
- **Skinler ve dünyalar** — Yerel skin paketleri, döndürülebilir 3B önizleme, favoriler, PNG dışa aktarma ve 64×64 veya 128×128 şeffaf PNG bindirme. Dünya kartları oyun içi görselleri gösterebilir ve `.mcworld` dosyaları Minecraft’a aktarılabilir.
- **CurseForge ve Mod Merkezi** — Kaynak ve davranış paketleri, dünyalar, skin paketleri ve Cinnabar shader’ları için ayrı kataloglar. Mod Merkezi’nde sürüm seçenekleri ve doğrulanmış DLL indirmeleri bulunur. CurseForge anahtarı gerekiyorsa katalogdaki **Bağlantı** penceresinden eklenebilir.
- **Başlatma ve görünüm** — Minecraft kurulumu farklı sürücülerde algılanır. Kurulum için Microsoft Store üzerinden WinGet denenir. Discord Rich Presence sürümü ve oyun süresini isteğe bağlı paylaşır. Türkçe ve İngilizce, açık ve koyu temalar ve Material You paletleri desteklenir.

## Windows kurulumu

[v0.16.10 Windows x64 kurulumu ve sürüm notları](https://github.com/kokluk/Bedrockloader/releases/tag/v0.16.10)

## Kaynaktan derleme

Windows ve .NET 8 SDK gerekir:

```powershell
dotnet build .\KoklukBedrockModloader.csproj -c Release
```

Inno Setup 6.7.3 ile kurulum paketi üretmek için:

```powershell
.\installer\build.ps1 -IsccPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

Kurulum çıktısı `installer-output/`, bağımsız uygulama dosyaları `installer-payload-v0.16.10/` altında oluşur.

## Kontroller

```powershell
dotnet run --project .\tests\PackLibrary.Tests -c Release
dotnet run --project .\tests\ReleaseUpdates.Tests -c Release -- .\installer-payload-v0.16.10\BedrockLoader.exe
```

İkinci kontrol için önce kurulum derleme betiğini çalıştır. Kontroller yükleyiciyi çalıştırmaz ve kullanıcının paketlerini değiştirmez.

## Modloader ve veriler

Ana ekrandaki **Modloader'ı kur**, kaynak URL'yi ve Content hedefini göstererek izin ister. Mevcut DLL değiştirilecekse ayrıca onay alınır. Harici DLL kaynak koduna veya kurulum paketine dahil değildir; uyumluluğu harici sağlayıcıya bağlıdır.

Kaynak: https://github.com/kokluk/BedrockFree/releases/download/McBedrock/vcruntime140_1.dll

Ayarlar/kayıtlar `%APPDATA%\BedrockLoader`, yerel modlar `%APPDATA%\Minecraft bedrock\mods` altında tutulur. Kaldırma, Minecraft paketlerini ve kullanıcı ayarlarını silmez.

Güncelleyicinin dağıtım için erişilebilir bir GitHub Release ve doğrulanabilir kurulum dosyası bulunması gerekir. Depo özel ise anonim erişimle güncelleme alınamaz.

MIT lisansı — Kokluk. Kaynak ağacında hazır EXE, DLL veya dağıtım ZIP'i tutulmaz; çalıştırılabilir paketler yalnızca Releases bölümünde paylaşılır.
