# PocketCam

PocketCam transforma um celular Android em webcam para Windows. O desktop encontra automaticamente o telefone, mantém todas as rotas disponíveis aquecidas e escolhe a melhor conexão: USB, Wi-Fi e, por último, Bluetooth. Se a conexão ativa cair, outra rota já conectada assume sem reiniciar a câmera.

## O que já faz parte do projeto

- captura no Android com CameraX, câmera frontal/traseira e controle de resolução, FPS e qualidade JPEG;
- protocolo binário versionado, com checksum, keep-alive e descarte de frames atrasados;
- conexão Wi-Fi por TCP de baixa latência e descoberta automática por NSD + beacon UDP;
- conexão USB por túnel ADB, escolhida acima do Wi-Fi quando o cabo está disponível;
- fallback Bluetooth Classic por RFCOMM, indicado para resoluções e FPS reduzidos;
- seleção contínua `USB > Wi-Fi > Bluetooth`, com histerese e failover imediato;
- preview e telemetria no aplicativo Windows;
- saída para câmera virtual Media Foundation no Windows 11;
- testes unitários do protocolo e do algoritmo de seleção, vetor binário Android/.NET e integração TCP loopback do desktop;
- CI para testes/builds e workflow de release com APK e pacote Windows.

## Requisitos

- Android 8.0 (API 26) ou superior;
- Windows 11 build 22000 ou superior para a câmera virtual; o preview funciona no Windows 10 19041+;
- .NET SDK 10 e Visual Studio Build Tools 2022 com o Windows 11 SDK para compilar o desktop;
- JDK 17 e Android SDK 35 para compilar o APK;
- para USB: depuração USB habilitada e o computador autorizado no telefone. O release do Windows procura `adb.exe` no pacote, no Android SDK local e no `PATH`.

## Desenvolvimento

```powershell
dotnet test PocketCam.sln -c Release
dotnet publish src/PocketCam.Desktop/PocketCam.Desktop.csproj -c Release -r win-x64 --self-contained true

Set-Location android
gradle :app:testDebugUnitTest :app:assembleDebug
```

Veja [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) para o desenho do sistema e [docs/PROTOCOL.md](docs/PROTOCOL.md) para o formato do fio. Instruções completas de uso e pareamento estão em [docs/USER_GUIDE.md](docs/USER_GUIDE.md).

## Releases

Tags no formato `v*` acionam `.github/workflows/release.yml`. O workflow produz:

- `PocketCam-Android.apk`, assinado quando os segredos de assinatura estão configurados;
- `PocketCam-Windows-win-x64.zip`, autocontido;
- checksums SHA-256 de todos os artefatos.

Consulte [docs/RELEASING.md](docs/RELEASING.md) antes do primeiro release.
