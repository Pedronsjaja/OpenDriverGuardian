# OpenDriver Guardian

Inventário e diagnóstico inicial de hardware, **local e somente leitura**, para Windows e Linux. Interface Avalonia e CLI C#/.NET 10. [English](README.en.md).

O MVP coleta dados reais do sistema; não apresenta porcentagem artificial de saúde. Não instala, remove, baixa ou atualiza drivers, BIOS ou firmware. Não há telemetria nem upload automático. Relatórios são salvos apenas por solicitação.

## O que existe

| Recurso | Estado / limite |
|---|---|
| Windows: sistema, CPU, placa-mãe, BIOS e memória | IMPLEMENTADO via CIM, conforme dados e permissões disponíveis |
| Windows: PnP e drivers associados | IMPLEMENTADO: Hardware IDs, classe, versão/provedor/INF e código de problema; metadado IsSigned não é validação Authenticode |
| Linux: sistema e PCI/USB | IMPLEMENTADO via proc/sysfs/os-release, IDs, modalias e binding; nomes podem ser apenas identificadores |
| Intel/AMD/NVIDIA | Inventário independente do fabricante; cobertura depende da enumeração do SO. Não significa teste em cada fabricante/modelo |
| Diagnóstico | Códigos Windows 28/22/outros são explicados. Sem binding Linux não comprova driver ausente |
| Interface desktop | Visão geral, inventário, diagnóstico, áudio observado, exportação e idioma pt/en |
| Exportação | JSON e HTML implementados; PDF planejado |
| Áudio | Apenas classificação de dispositivos observados; sem captura/reprodução, teste de rota ou endpoint padrão |
| Matching, consultas de atualização, download/instalação e rollback | PLANEJADOS, sem botões que simulem execução |
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

O driver instalado não foi comparado a um catálogo oficial de candidatos. Não declarar atualização compatível pela existência de uma versão maior; não remover pacotes sem associação por suposição. Firmware terá fluxo separado. Os scripts do projeto não alteram privilégios, serviços ou dispositivos. [Segurança](SECURITY.md).

## Desenvolvimento e licença

Fontes em `src/`, testes em `tests/`, CI em `.github/workflows/`. Não há dependência de provedor de IA ou serviço pago. [Contribuição](CONTRIBUTING.md) · [Decisões](docs/architecture/decisions.md) · [Licenças e referências externas](THIRD_PARTY_NOTICES.md).

Código original sob [MIT](LICENSE). Referências externas foram estudadas conceitualmente; nenhum driver, código GPL ou pacote de atualização foi importado.
