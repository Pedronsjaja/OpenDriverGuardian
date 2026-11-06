# OpenDriver Guardian

Inventário e diagnóstico inicial de hardware, **local e somente leitura**, para Windows e Linux. Interface Avalonia e CLI C#/.NET 10. [English](README.en.md).

O projeto coleta dados reais do sistema; não apresenta porcentagem artificial de saúde. A tela **Drivers e Downloads** permite consultar fontes e baixar conteúdo elegível por solicitação. Não instala, remove ou atualiza drivers, BIOS ou firmware. Sem telemetria ou upload automático de inventários. A consulta online ao Windows Update exige consentimento explícito na tela.

## O que existe

| Recurso | Estado / limite |
|---|---|
| Windows: sistema, CPU, placa-mãe, BIOS e memória | IMPLEMENTADO via CIM, conforme dados e permissões disponíveis |
| Windows: PnP e drivers associados | IMPLEMENTADO: Hardware IDs/Compatible IDs, classe, versão/provedor/INF e código de problema; metadado IsSigned não é validação Authenticode |
| Linux: sistema e PCI/USB | IMPLEMENTADO via proc/sysfs/os-release, IDs, modalias e binding; nomes podem ser apenas identificadores |
| Intel/AMD/NVIDIA | Inventário independente do fabricante; cobertura depende da enumeração do SO. Não significa teste em cada fabricante/modelo |
| Diagnóstico | Códigos Windows 28/22/outros são explicados. Sem binding Linux não comprova driver ausente |
| Interface desktop | Visão geral, inventário, diagnóstico, áudio observado, exportação e idioma pt/en |
| Exportação | JSON e HTML implementados; PDF planejado |
| Áudio | Apenas classificação de dispositivos observados; sem captura/reprodução, teste de rota ou endpoint padrão |
| Windows Update | Adaptador WUA implementado; consulta online real ainda NÃO VALIDADA. Sem contornar políticas |
| Matching | IDs completos + SO/versão/build/arquitetura + aplicabilidade WUA. OEM/subsystem delegado ao WUA; sem inspeção independente de INF |
| Downloads | HTTPS oficial, redirects restritos, progresso/cancelamento, SHA-256, Authenticode e registro local. Validado com servidor local de teste; sem download real de driver como evidência |
| Ubuntu | Recomendações locais de `ubuntu-drivers devices`, se instalado. Pacotes não verificados; download/instalação manuais |
| Fabricantes / Catalog | Páginas oficiais para consulta manual. Sem scraping, API universal ou URLs de arquivo presumidas |
| Instalação e rollback | PLANEJADOS; não implementados |
| SQLite/histórico | PLANEJADO; sem banco desnecessário para o scan atual |

Leia [arquitetura e limites](docs/architecture/overview.md), [fontes](docs/research/references.md), [validação](docs/validation.md) e [roadmap](docs/roadmap/backlog.md). A evidência distingue compilação, testes de fixtures e execução real por plataforma.

## Compilar e executar

Instale o SDK .NET 10 compatível com `global.json` (10.0.403; atualizações de patch permitidas). Python não é necessário. Os pacotes NuGet são restaurados na primeira compilação; o scan funciona offline após a instalação.

```bash
dotnet restore OpenDriverGuardian.sln
dotnet build OpenDriverGuardian.sln -c Release --no-restore
dotnet test OpenDriverGuardian.sln -c Release --no-build
dotnet run --project src/OpenDriverGuardian.Cli -- scan
dotnet run --project src/OpenDriverGuardian.Cli -- scan --output report.json --html report.html
dotnet run --project src/OpenDriverGuardian.App
```

Na CLI, escolha nomes novos para exportar: arquivos existentes não são sobrescritos. Códigos de saída: 0 coleta sem avisos; 1 coleta parcial/com avisos (inclusive limites de escopo); 2 erro; 3 cancelamento/timeout. Aviso não equivale a defeito de hardware. Linux usa apenas leitura de atributos de texto; não requer lspci, lsusb ou root para o conjunto básico. A GUI Linux precisa de ambiente gráfico e bibliotecas nativas do Avalonia: [plataformas suportadas](https://docs.avaloniaui.net/docs/supported-platforms).

## Privacidade e interpretação

Não são solicitados números de série, contas, MAC ou arquivos pessoais. Chaves PnP de instância são transformadas em hash antes da saída do adaptador Windows. Hardware IDs e rótulos de dispositivos ainda podem identificar a configuração da máquina: revise antes de compartilhar. Relatórios completos reais não são fixtures públicas.

O scan não consulta catálogos nem baixa arquivos. Na GUI: analise → abra **Drivers e Downloads** → autorize a consulta → revise as evidências → baixe um candidato elegível. A Microsoft pode receber dados de aplicabilidade pela consulta WUA. Fabricantes sem integração permanecem em consulta manual.

Downloads ficam em `%LOCALAPPDATA%/OpenDriverGuardian/Downloads/<operação>/`, com `result.json`. Hash calculado sem referência confiável não comprova integridade. Assinatura indisponível mantém o arquivo com `.unverified`; hash divergente/assinatura inválida remove o payload. A aplicação nunca executa o arquivo. Conteúdo WUA com HTTP, múltiplos arquivos ou URL ausente não habilita download. Versão desconhecida: `UNKNOWN`; tamanho WUA é estimativa.

Leia [implementação e limites da Fase 3](docs/architecture/driver-discovery.md). `apt`/`dnf`/`pacman`, APIs de fabricantes, validação interna de CAB/INF e firmware/LVFS não estão integrados. Não recomendar atualização pela existência de uma versão maior. [Segurança](SECURITY.md).

## Desenvolvimento e licença

Fontes em `src/`, testes em `tests/`, CI em `.github/workflows/`. Não há dependência de provedor de IA ou serviço pago. [Contribuição](CONTRIBUTING.md) · [Decisões](docs/architecture/decisions.md) · [Licenças e referências externas](THIRD_PARTY_NOTICES.md).

Código original sob [MIT](LICENSE). Referências externas foram estudadas conceitualmente; nenhum driver, código GPL ou pacote de atualização foi importado.
