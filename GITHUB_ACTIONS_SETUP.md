# GitHub Actions - Build and Release MSIX

Este workflow automatiza o build, assinatura e lançamento do MSIX no GitHub Actions.

## ?? Pré-requisitos

Antes de usar este workflow, você precisa:

### 1. Gerar um Certificado (se ainda não tiver)

Se você já possui um certificado `.pfx`, pule para a etapa 2.

**Opção A: Gerar um certificado auto-assinado (Desenvolvimento)**
```powershell
# No PowerShell como Administrator
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=FNFVsliceLauncher" -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5)

# Exportar para arquivo .pfx
$password = ConvertTo-SecureString -String "sua_senha_super_secreta" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "certificado.pfx" -Password $password
```

**Opção B: Certificado Profissional (Produção)**
- Adquira um certificado de autenticação de código de uma autoridade certificadora confiável (Sectigo, DigiCert, etc.)
- Certifique-se de que está em formato `.pfx`

### 2. Codificar o Certificado em Base64

Execute este comando PowerShell para codificar seu certificado:

```powershell
$cert = [System.IO.File]::ReadAllBytes("caminho\para\certificado.pfx")
$certBase64 = [System.Convert]::ToBase64String($cert)
$certBase64 | Set-Clipboard
```

O certificado codificado estará na sua área de transferência.

### 3. Adicionar Secrets no GitHub

1. Vá para seu repositório no GitHub
2. Clique em **Settings** ? **Secrets and variables** ? **Actions**
3. Clique em **New repository secret**
4. Adicione os seguintes secrets:

| Nome | Valor |
|------|-------|
| `SIGNING_CERTIFICATE` | Cole aqui o certificado base64 (da etapa 2) |
| `CERTIFICATE_PASSWORD` | A senha do seu certificado `.pfx` |

## ?? Como Usar

### Iniciar um Build Manual
1. Vá em **Actions** no seu repositório
2. Selecione o workflow **Build and Release MSIX**
3. Clique em **Run workflow**

### Iniciar um Build Automático (Recomendado)
Simplesmente crie uma tag e faça push:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Isso automaticamente:
- ? Builda o projeto para x64, x86 e ARM64
- ? Assina cada MSIX com seu certificado
- ? Cria um release no GitHub
- ? Faz upload dos MSIXs para a página de releases

## ?? Outputs

Os arquivos MSIX assinados estarão disponíveis em:
- **GitHub Actions Artifacts**: Na aba "Actions" do seu repositório
- **GitHub Releases**: Na página de releases do seu repositório

## ?? Segurança

- O certificado é criptografado no GitHub e nunca é exposto em logs
- A senha é tratada como um secret sensível
- Use certificados diferentes para desenvolvimento (auto-assinados) e produção (CAs confiáveis)

## ?? Troubleshooting

### Erro: "Certificate not found"
- Verifique se os secrets foram adicionados corretamente
- Teste o base64 decodificando: `[System.Convert]::FromBase64String($certificateBase64) | Set-Content test.pfx -AsByteStream`

### Erro: "MSIX not found"
- Verifique se `EnableMsixTooling` está habilitado no `.csproj`
- Confirme que o projeto compila localmente com `dotnet publish`

### Erro: "Invalid signature"
- Verifique se a senha do certificado está correta
- Confirme que o certificado é válido e não expirou

## ?? Estrutura do Workflow

```
build-and-release.yml
??? Build Job (matrix: x64, x86, ARM64)
?   ??? Checkout código
?   ??? Setup .NET 8
?   ??? Restaurar dependências
?   ??? Build MSIX
?   ??? Importar certificado
?   ??? Assinar MSIX
?   ??? Upload artifacts
??? Release Job (depende de Build)
    ??? Download artifacts
    ??? Criar release
    ??? Upload para GitHub Releases
```

## ?? Referências

- [Documentação SignTool](https://docs.microsoft.com/en-us/windows/win32/seccrypto/signtool)
- [GitHub Actions Secrets](https://docs.microsoft.com/en-us/actions/security-guides/using-secrets-in-github-actions)
- [MSIX Packaging](https://docs.microsoft.com/en-us/windows/msix/)
