# Hikvision Abrir Catraca

Aplicativo Windows simples para a operação abrir as catracas do AcquaVale sem precisar acessar o HikCentral Professional.

## Organização operacional

A tela principal é dividida exatamente pelos Access Levels existentes no HikCentral:

- **ENTRADA ACQUAVALE** — catracas associadas aos equipamentos/áreas 192.168.104.12, .13, .14 e .15.
- **SAIDA ACQUAVALE** — 192.168.104.22, .23 e .24.
- **CATRACAS SABIA** — 192.168.81.177 e .178.
- **LOJA ACQUAVALE** — 192.168.104.89.

> Os IPs acima são somente referência visual da configuração atual. O aplicativo **não usa IP como identificador de porta**. Em cada abertura ele consulta o Access Level no HikCentral e usa os `Element.ID` reais retornados pela OpenAPI.

## Como funciona

1. O operador clica em um dos quatro botões grandes.
2. O aplicativo consulta `POST /artemis/api/acs/v1/privilege/group`.
3. Localiza o Access Level pelo nome.
4. Extrai os IDs das portas/catracas em `ElementList[].Element.ID`.
5. Envia `POST /artemis/api/acs/v1/door/doControl` com `controlType=2` (open).
6. Mostra sucesso/falha de cada porta na própria tela.

A implementação usa a mesma assinatura Artemis/OpenAPI já usada no projeto `integracao_catracas_hikivision`.

## Configuração no HikCentral

Crie/edite a aplicação OpenAPI utilizada pelo projeto e autorize, no mínimo:

- `POST /artemis/api/acs/v1/privilege/group`
- `POST /artemis/api/acs/v1/door/doControl`

Não é necessário usar usuário/senha do Web Client. O aplicativo usa **AppKey + AppSecret** da OpenAPI.

## Primeiro uso

1. Abra **Configurações**.
2. Informe a URL do HikCentral, por exemplo `https://127.0.0.1` ou o IP/hostname do servidor.
3. Informe AppKey, AppSecret e User ID.
4. Use **Testar conexão e grupos**.
5. Salve.

O AppSecret é armazenado localmente com DPAPI (escopo do usuário do Windows), não no repositório.

## Compilação automática no GitHub

O workflow `.github/workflows/build-windows.yml` gera um executável Windows self-contained x64 e publica o ZIP como artifact do GitHub Actions.

Caminho no GitHub:

**Actions > Build Windows EXE > última execução > Artifacts > HikvisionAbrirCatraca-win-x64**

## Compilar pelo VS Code / PowerShell

Se preferir compilar localmente:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Saída:

```
dist\HikvisionAbrirCatraca.exe
```

Também pode executar diretamente:

```powershell
dotnet run --project .\src\HikvisionAbrirCatraca\HikvisionAbrirCatraca.csproj
```

## Segurança operacional

- Abertura é de pulso normal; o projeto **não usa** `remain open`.
- O botão fica temporariamente bloqueado enquanto o comando está sendo processado.
- Há timeout de rede e log local das operações.
- Credenciais reais não fazem parte do Git.
- A opção de validação TLS deve ficar ligada quando o servidor tiver certificado confiável.

## Observação sobre direção

Versões atuais do HikCentral exigem `controlDirection`: 0 = entrada e 1 = saída. Por padrão os quatro grupos usam 0, pois cada Access Level já aponta para catracas físicas separadas. Se algum torniquete exigir direção 1, altere apenas esse grupo em **Configurações**; não é necessário recompilar.


## Administração de catracas

A versão atual possui uma área **Administração** protegida por senha.

No primeiro acesso, o sistema solicita a criação da senha administrativa. A senha não é armazenada em texto puro: é persistido somente um hash PBKDF2 com salt.

A tela administrativa permite:

- adicionar catraca manualmente;
- editar nome, Door Index Code, IP/referência, grupo, direção e status ativo/inativo;
- excluir catraca;
- testar a abertura de uma única catraca;
- sincronizar/importar automaticamente as catracas dos quatro Access Levels do HikCentral;
- alterar a senha administrativa.

### Regra de operação

Quando um grupo ainda não possui cadastro local, o botão principal continua descobrindo as catracas pelo Access Level do HikCentral, como antes.

Depois que houver catracas cadastradas/importadas para um grupo, o aplicativo passa a usar **somente as catracas locais marcadas como Ativas**. Isso permite retirar temporariamente uma catraca da operação sem alterar o HikCentral.

A sincronização usa o `Door Index Code` como chave para evitar duplicidade.

### Grupos mantidos

- ENTRADA ACQUAVALE
- SAIDA ACQUAVALE (também reconhece o nome legado SAIDA AQCUAVALE)
- CATRACAS SABIA
- LOJA ACQUAVALE
