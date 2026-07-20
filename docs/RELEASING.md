# Publicação de releases

O workflow usa ambientes limpos do GitHub Actions e publica APK, pacote Windows e checksums quando uma tag `v*` é enviada.

## Assinatura Android

Configure estes secrets no repositório:

- `ANDROID_KEYSTORE_BASE64`: keystore codificado em base64;
- `ANDROID_KEY_ALIAS`;
- `ANDROID_KEY_PASSWORD`;
- `ANDROID_STORE_PASSWORD`.

Sem eles, o workflow produz um APK de release assinado com chave efêmera de CI e o marca claramente como não destinado à Play Store.

## Criar um release

```powershell
git tag v0.1.0
git push origin v0.1.0
```

O job só cria o GitHub Release se testes Android, testes .NET e ambos os builds terminarem com sucesso.

