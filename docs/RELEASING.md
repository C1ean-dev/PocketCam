# Publicação de releases

O workflow usa ambientes limpos do GitHub Actions e publica APK, pacote Windows e checksums quando uma tag `v*` é enviada. A tag precisa apontar exatamente para o commit atual da `main` remota; caso contrário, o workflow para antes dos testes e builds. Os dois artefatos usam o SHA da `main` validado no início da execução.

## Assinatura Android

Configure estes secrets no repositório:

- `ANDROID_KEYSTORE_BASE64`: keystore codificado em base64;
- `ANDROID_KEY_ALIAS`;
- `ANDROID_KEY_PASSWORD`;
- `ANDROID_STORE_PASSWORD`.

Sem eles, o workflow produz um APK de release assinado com chave efêmera de CI e o marca claramente como não destinado à Play Store.

## Criar um release

```powershell
git switch main
git pull --ff-only origin main
git push origin main
git tag v0.1.0
git push origin v0.1.0
```

O job só cria o GitHub Release se a tag corresponder à `main`, os testes Android e .NET passarem e ambos os builds terminarem com sucesso. Envie a `main` antes da tag; enviar somente uma tag para um commit local ainda ausente da `main` remota será rejeitado.

O workflow deriva `Version`/`versionName` da tag e usa o número da execução como `versionCode` do APK. Mantenha as tags no formato semântico `vMAJOR.MINOR.PATCH`, pois os verificadores de atualização comparam esses três componentes.
