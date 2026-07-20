# PocketCam Wire Protocol v1

Todos os inteiros são little-endian. Cada mensagem começa com um cabeçalho fixo de 28 bytes.

| Offset | Tamanho | Campo |
|---:|---:|---|
| 0 | 4 | magic ASCII `PCM1` |
| 4 | 1 | versão (`1`) |
| 5 | 1 | tipo |
| 6 | 2 | flags |
| 8 | 4 | tamanho do payload, máximo 16 MiB |
| 12 | 4 | sequência |
| 16 | 8 | timestamp Unix em microssegundos |
| 24 | 4 | CRC32 IEEE do payload |

Tipos: `HELLO=1`, `FRAME=2`, `SETTINGS=3`, `PING=4`, `PONG=5`, `STATUS=6`, `ERROR=255`.

`HELLO`, `SETTINGS`, `STATUS` e `ERROR` usam UTF-8 JSON. `PING` e `PONG` não precisam de payload.

## Payload FRAME

| Offset | Tamanho | Campo |
|---:|---:|---|
| 0 | 2 | largura |
| 2 | 2 | altura |
| 4 | 2 | rotação em graus (`0`, `90`, `180`, `270`) |
| 6 | 1 | codec (`1` = JPEG) |
| 7 | 1 | reservado |
| 8 | N | bytes codificados |

O receptor deve sempre validar magic, versão, tamanho, rotação e CRC antes de usar o payload.

