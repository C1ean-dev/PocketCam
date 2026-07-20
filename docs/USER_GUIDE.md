# Guia de uso

1. Instale o APK no Android, extraia o pacote PocketCam no Windows e execute `PocketCam.exe`.
2. Abra os dois aplicativos. Conceda câmera e, se usar Bluetooth, permissão para dispositivos próximos.
3. Na mesma rede Wi-Fi, o telefone deve aparecer automaticamente em poucos segundos.
4. Para USB, habilite **Opções do desenvolvedor > Depuração USB**, conecte o cabo e aceite a autorização RSA. O desktop cria o túnel automaticamente.
5. Para Bluetooth, pareie telefone e computador nas configurações do sistema antes de abrir os aplicativos.
6. Escolha resolução, FPS, qualidade e lente no Android ou no painel Windows.
7. No primeiro uso, clique em **Instalar / reparar câmera** e aceite a elevação do Windows. Reabra o aplicativo de videoconferência caso ele já estivesse em execução.
8. Em aplicativos como Teams, Zoom, navegadores ou OBS, selecione **PocketCam Virtual Camera** (Windows 10) ou **PocketCam** (Windows 11).
9. Windows e Android verificam silenciosamente a release estável mais recente ao abrir. Se houver atualização, confirme para abrir o download correspondente. Também é possível usar **Verificar atualizações** no painel de cada aplicativo.

O indicador de transporte fica verde para a rota ativa. USB tem preferência. Ao remover o cabo, uma sessão Wi-Fi já saudável assume automaticamente; Bluetooth é usado quando as outras duas não estão disponíveis.

Para reduzir travamentos em redes congestionadas, use 1280×720 a 20 FPS. Bluetooth deve ficar em 320×240 a 5 FPS.

## Solução de problemas

- **USB não aparece:** execute `adb devices`, desbloqueie o telefone e aceite a autorização. Confira também se o driver USB do fabricante está instalado.
- **Wi-Fi não aparece:** confirme que a rede permite comunicação entre clientes; redes de convidados geralmente bloqueiam multicast e broadcast.
- **Bluetooth não aparece:** pareie os dispositivos no Windows e reabra os dois aplicativos.
- **A câmera virtual não aparece:** confirme Windows 10 build 19041+, habilite o acesso à câmera em Privacidade e segurança e clique novamente em **Instalar / reparar câmera**. Reinicie o aplicativo consumidor após a instalação. O pacote é x64 e não aparece em aplicativos de vídeo 32 bits.
- **A verificação de atualização falha:** confirme o acesso a `api.github.com` e tente novamente pelo botão. Essa falha não afeta a transmissão da câmera.
