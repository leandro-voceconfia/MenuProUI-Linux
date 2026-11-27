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
- [x] `.deb` gerado: `menupro-ui_2.0.8_amd64.deb`
- [x] `.msi` gerado para Windows x64 pelo workflow `Windows MSI release`

## 3) Publicação no GitHub
- [x] Criar tag `v2.0.8`
- [x] Criar release "MenuProUI-Linux 2.0.8"
- [x] Anexar `.deb` e `.msi`
- [x] Incluir changelog da compatibilidade Windows

## 4) Script de automação
- [x] Informar owner/repo como argumento, sem `.env.release`
- [x] Rodar script: `bash scripts/release_publish_linux.sh 2.0.8 leandro-voceconfia/MenuProUI`
