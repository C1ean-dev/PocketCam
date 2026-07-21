# PocketCam

PocketCam transforma um celular Android em webcam para Windows. O desktop encontra automaticamente o telefone, mantém todas as rotas disponíveis aquecidas e escolhe a melhor conexão: USB, Wi-Fi e, por último, Bluetooth. Se a conexão ativa cair, outra rota já conectada assume sem reiniciar a câmera.

## O que já faz parte do projeto

- captura no Android com CameraX, câmera frontal/traseira e controle de resolução, FPS e qualidade JPEG;
- protocolo binário versionado, com checksum, keep-alive e descarte de frames atrasados;
- conexão Wi-Fi por TCP de baixa latência e descoberta automática por NSD + beacon UDP;
- descoberta Wi-Fi resiliente por anúncio passivo, solicitação UDP ativa e sondagem autenticada da rede local;
- conexão USB por túnel ADB, escolhida acima do Wi-Fi quando o cabo está disponível;
- fallback Bluetooth Classic por RFCOMM, indicado para resoluções e FPS reduzidos;
- seleção contínua `USB > Wi-Fi > Bluetooth`, com histerese e failover imediato;
- sessões de transporte isoladas: uma sondagem, desconexão ou troca de rota não encerra o serviço Android;
- preview e telemetria no aplicativo Windows;
- câmera virtual DirectShow no Windows 10 e Media Foundation no Windows 11, escolhida automaticamente;
- verificação automática e manual da release estável mais recente, com confirmação antes de abrir o ZIP no Windows ou o APK no Android;
- aviso no Android ao detectar cabo com Depuração USB desativada, com atalho direto para as Opções do desenvolvedor;
- testes unitários do protocolo e do algoritmo de seleção, vetor binário Android/.NET e integração TCP loopback do desktop;
- CI para testes/builds e workflow de release com APK e pacote Windows.

## Requisitos

- Android 8.0 (API 26) ou superior;
- Windows 10 versão 2004 (build 19041) ou superior, incluindo a câmera virtual; Windows 11 usa o backend Media Foundation mais moderno;
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

A versão gravada nos binários é derivada da própria tag. Os aplicativos consultam a release pública mais recente ao iniciar; falhas de rede não interrompem a câmera e nenhum download ou instalação acontece sem confirmação.

Consulte [docs/RELEASING.md](docs/RELEASING.md) antes do primeiro release.
