# BedrockLoader kurulumu

`BedrockLoader-Setup-v0.15.12-x64.exe`, Windows 10/11 x64 için tek dosyalık kurulum paketidir. Uygulamayı varsayılan olarak `%LOCALAPPDATA%\Programs\BedrockLoader` klasörüne kurar ve yönetici izni gerektirmez.

- Türkçe ve İngilizce kurulum sihirbazı.
- Windows'un açık/koyu uygulama temasını otomatik izleyen kurulum ve kaldırma sihirbazı; koyu görünümde koyu gri zemin (#202020).
- Başlat menüsü kısayolu; isteğe bağlı masaüstü kısayolu.
- Windows uygulama listesinden kaldırma desteği.
- .NET 8 çalışma zamanı pakete dahildir; ayrıca .NET kurulması gerekmez.
- Minecraft modları, Content klasörüne ayrıca kurulan modloader ve AppData ayarları kaldırma sırasında silinmez.
- Harici modloader DLL'i pakete dahil değildir; uygulamadaki izinli kurulum akışı geçerlidir.

## Yeniden üretme

Windows'ta .NET 8 SDK ve Inno Setup 6.7.3 veya uyumlu sürümü gerekir. Proje kökünden:

```powershell
.\installer\build.ps1 -IsccPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

Çıktı: `installer-output/BedrockLoader-Setup-v0.15.12-x64.exe`.

Derleme betiği proje dosyalarını önce `BedrockLoader-v0.12/` klasöründe, ardından depo kökünde arar.

Derleyici: [Inno Setup resmi indirmeleri](https://jrsoftware.org/isdl.php). Kurulum paketi dijital olarak imzalanmamıştır.

## Doğrulama

```powershell
.\installer\smoke-test.ps1
```

Test, mevcut BedrockLoader kurulumu varsa durur. Yoksa proje içinde benzersiz bir test klasörüne sessiz kurulum yapar, tüm uygulama dosyalarını SHA-256 ile karşılaştırır, gömülü çalışma zamanını ve Windows kaldırma kaydını doğrular. Ardından test kurulumunu kaldırır ve kaldırma kaydının silindiğini kontrol eder. Başlat menüsü/masaüstü kısayolları test sırasında oluşturulmaz.






