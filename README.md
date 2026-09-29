# Hikvision Abrir Catraca

Aplicativo Windows para abrir diretamente as catracas Hikvision do AcquaVale, sem depender do HikCentral Professional.

## Arquitetura atual

O HikCentral não participa mais do fluxo de abertura.

\`\`\`text
Aplicativo Windows
        |
        | HTTP/HTTPS + Digest Authentication
        v
Catraca Hikvision
        |
        | ISAPI
        v
/ISAPI/AccessControl/RemoteControl/door/{doorNo}
\`\`\`

O comando usado para uma abertura normal é um \`PUT\` com XML:

\`\`\`xml
<RemoteControlDoor version="2.0" xmlns="http://www.isapi.org/ver20/XMLSchema">
  <cmd>open</cmd>
</RemoteControlDoor>
\`\`\`

Não existe AppKey, AppSecret ou OpenAPI do HikCentral nesta versão.

## Grupos operacionais

A tela principal continua separada como solicitado:

- **ENTRADA ACQUAVALE** — referência atual: 192.168.104.12, .13, .14 e .15
- **SAIDA ACQUAVALE** — referência atual: 192.168.104.22, .23 e .24
- **CATRACAS SABIA** — referência atual: 192.168.81.177 e .178
- **LOJA ACQUAVALE** — referência atual: 192.168.104.89

O cadastro real de cada equipamento é feito na área **Administração**.

## Administração

A área administrativa é protegida por senha.

No primeiro acesso o aplicativo solicita a criação da senha administrativa.

Para cada catraca podem ser configurados:

- nome amigável;
- IP ou hostname;
- porta HTTP/HTTPS;
- HTTP ou HTTPS;
- validação do certificado TLS;
- usuário da própria catraca;
- senha da própria catraca;
- Door Nº usado pelo ISAPI;
- grupo operacional;
- ativa/inativa.

A tela permite:

- adicionar;
- editar;
- excluir;
- ativar/desativar;
- testar conexão sem abrir;
- testar abertura real individual;
- testar conexão com todas as catracas;
- alterar senha administrativa.

## Teste de conexão

O teste consulta diretamente:

\`\`\`text
GET /ISAPI/System/deviceInfo
\`\`\`

e mostra, quando o dispositivo fornece, modelo e versão de firmware.

Isso permite validar IP, porta, usuário e senha sem acionar a catraca.

## Abertura

A operação funciona em duas etapas:

1. o operador seleciona o bloco/grupo;
2. o aplicativo mostra as catracas ativas daquele bloco.

Dentro do bloco existem duas formas de operação:

- **ABRIR ESTA CATRACA** — envia o comando somente para a catraca escolhida;
- **ABRIR TODAS** — envia o comando para todas as catracas ativas daquele bloco.

O botão **ABRIR TODAS** fica no topo da seção e exige confirmação antes de executar, para reduzir risco de acionamento acidental.

Exemplo:

\`\`\`text
ENTRADA ACQUAVALE                            [ ABRIR TODAS ]

Entrada 01   192.168.104.12   [ ABRIR ESTA CATRACA ]
Entrada 02   192.168.104.13   [ ABRIR ESTA CATRACA ]
Entrada 03   192.168.104.14   [ ABRIR ESTA CATRACA ]
Entrada 04   192.168.104.15   [ ABRIR ESTA CATRACA ]
\`\`\`

Na abertura em lote, os comandos são enviados em paralelo. Se uma catraca estiver offline, as demais continuam sendo acionadas e a tela informa quais falharam.

## Autenticação

A integração direta usa a conta local do próprio equipamento Hikvision através de HTTP Digest Authentication.

A senha de cada catraca é armazenada no Windows usando DPAPI no escopo do usuário atual. Ela não é publicada no GitHub nem fica gravada em texto puro no arquivo de configuração.

A senha da área administrativa é armazenada como hash PBKDF2 com salt.

## Configuração inicial

1. Execute o aplicativo.
2. Abra **Administração**.
3. Crie a senha administrativa.
4. Clique em **Adicionar**.
5. Informe IP, porta, usuário e senha da catraca.
6. Deixe **Door Nº = 1** inicialmente.
7. Escolha o grupo operacional.
8. Salve.
9. Use **Testar conexão**.
10. Depois use **Testar abertura**.
11. Repita para as demais catracas.

Depois dos testes individuais, os botões grandes da tela operacional ficam prontos para uso.

## Door Nº

Na maioria dos terminais/controladores com uma única porta o valor inicial é \`1\`.

Equipamentos com mais de uma porta/relé podem utilizar \`2\`, \`3\` etc. O valor é configurável por catraca sem recompilar o aplicativo.

## Segurança operacional

- o aplicativo usa apenas o comando \`open\`;
- não usa \`alwaysOpen\`;
- a abertura individual abre somente uma catraca;
- a abertura em lote só ocorre pelo botão **ABRIR TODAS** dentro do bloco e exige confirmação;
- uma catraca pode ser desativada sem ser excluída;
- há timeout de rede;
- todos os acionamentos são registrados no log local;
- a área de cadastro é protegida por senha;
- credenciais não são versionadas no Git.

## Compilação automática

O workflow:

\`\`\`text
.github/workflows/build-windows.yml
\`\`\`

gera uma versão Windows x64 self-contained.

No GitHub:

**Actions > Build Windows EXE > Artifacts > HikvisionAbrirCatraca-win-x64**

## Compilar no VS Code

\`\`\`powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
\`\`\`

Saída:

\`\`\`text
dist\HikvisionAbrirCatraca.exe
\`\`\`

## Observação sobre compatibilidade

A integração foi feita usando a interface ISAPI padrão de controle de acesso da Hikvision. O suporte ao endpoint de abertura depende do modelo e firmware da catraca/controladora.

O botão **Testar conexão** identifica o equipamento sem abrir a porta. O botão **Testar abertura** deve ser usado durante a implantação para confirmar o Door Nº correto em cada modelo.


## Assinatura digital do Windows

O build está preparado para assinar o EXE com **Authenticode** depois do \`dotnet publish\`.

Fluxo:

\`\`\`text
restore
→ publish win-x64 self-contained single-file
→ assinatura Authenticode SHA-256
→ timestamp RFC 3161
→ signtool verify /pa
→ artifact final
\`\`\`

Se nenhum certificado estiver configurado, o build continua normalmente e informa que o EXE ficou sem assinatura. Se uma configuração de assinatura existir e a assinatura ou a validação falhar, o build é interrompido.

O script responsável é:

\`\`\`text
scripts\sign.ps1
\`\`\`

### Assinar localmente usando certificado instalado no Windows

Configure o thumbprint antes de executar o build:

\`\`\`powershell
$env:CODE_SIGN_CERT_THUMBPRINT="THUMBPRINT_DO_CERTIFICADO"
$env:CODE_SIGN_CERT_STORE="CurrentUser"
$env:CODE_SIGN_TIMESTAMP_URL="http://timestamp.digicert.com"

.\build.ps1
\`\`\`

Para certificado instalado em \`LocalMachine\My\`:

\`\`\`powershell
$env:CODE_SIGN_CERT_STORE="LocalMachine"
\`\`\`

### Assinar localmente usando PFX

O arquivo PFX não deve ser adicionado ao Git.

\`\`\`powershell
$env:CODE_SIGN_PFX_PATH="C:\Certificados\codesign.pfx"
$env:CODE_SIGN_PFX_PASSWORD="SENHA_DO_PFX"
$env:CODE_SIGN_TIMESTAMP_URL="http://timestamp.digicert.com"

.\build.ps1
\`\`\`

Arquivos \`.pfx\`, \`.p12\`, \`.pem\` e \`.key\` estão bloqueados pelo \`.gitignore\`.

### GitHub Actions

Para assinar automaticamente no GitHub Actions usando PFX, configure em:

**Settings > Secrets and variables > Actions**

Secrets:

\`\`\`text
CODE_SIGN_PFX_BASE64
CODE_SIGN_PFX_PASSWORD
\`\`\`

\`CODE_SIGN_PFX_BASE64\` deve conter o PFX convertido para Base64. Exemplo no PowerShell:

\`\`\`powershell
[Convert]::ToBase64String(
    [IO.File]::ReadAllBytes("C:\Certificados\codesign.pfx")
) | Set-Clipboard
\`\`\`

Cole o conteúdo resultante no secret \`CODE_SIGN_PFX_BASE64\`.

Variáveis opcionais do repositório:

\`\`\`text
CODE_SIGN_TIMESTAMP_URL = http://timestamp.digicert.com
CODE_SIGN_CERT_STORE    = CurrentUser
CODE_SIGN_REQUIRED      = true
\`\`\`

Não defina \`CODE_SIGN_REQUIRED=true\` enquanto o certificado ainda não estiver configurado. Sem essa variável, o workflow continua produzindo um artifact não assinado.

Depois que o certificado estiver configurado, recomenda-se definir:

\`\`\`text
CODE_SIGN_REQUIRED=true
\`\`\`

A partir daí qualquer falha de assinatura ou validação impede a publicação do artifact.

### Verificação manual

Depois da compilação:

\`\`\`powershell
Get-AuthenticodeSignature .\dist\HikvisionAbrirCatraca.exe | Format-List
\`\`\`

ou com o Windows SDK:

\`\`\`powershell
signtool verify /pa /all /v .\dist\HikvisionAbrirCatraca.exe
\`\`\`

O resultado esperado depois da configuração do certificado é \`Valid\`, exibindo o produtor do certificado de Code Signing.
