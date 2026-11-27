# Release Checklist — MenuProUI 2.0.8

> Este checklist é validado automaticamente por `scripts/release_preflight.sh`
> antes da publicação iniciada por `scripts/release_publish_linux.sh`.

## 1) Validação local
- [ ] Rodar app e testar atalhos principais (`Enter`, `Ctrl+N`, `Ctrl+Shift+N`, `Ctrl+R`, `Ctrl+Shift+K`, `Ctrl+Shift+D`)
- [ ] Confirmar abertura de SSH, RDP e URL
- [ ] Confirmar coluna `Net` nos acessos e indicador agregado na lista de clientes

## 2) Build e artefatos
- [x] Build release: `dotnet build`
- [x] Publish linux-x64: `dotnet publish MenuProUI.csproj -c Release -r linux-x64 --self-contained true -o publish/linux-x64`
- [ ] `.deb` gerado: `menupro-ui_2.0.8_amd64.deb`
- [ ] `.msi` gerado para Windows x64 pelo workflow `Windows MSI release`

## 3) Publicação no GitHub
- [ ] Criar tag `v2.0.8`
- [ ] Criar release "MenuProUI-Linux 2.0.8"
- [ ] Anexar `.deb` e `.msi`
- [ ] Incluir changelog curto (build Windows e instalador MSI)

## 4) Script de automação
- [ ] Copiar ambiente: `cp .env.release.example .env.release`
- [ ] Rodar script: `bash scripts/release_publish_linux.sh 2.0.8`
