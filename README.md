# CRM CDL Uberlândia

CRM próprio da equipe comercial da CDL Uberlândia, substituindo a Kommo.
Mesma tecnologia do site: **C# / .NET 8, ASP.NET Core MVC, Entity Framework Core 8 e MySQL 8 (Pomelo)**.
Visual com as cores do novo site (`desing-novo-site---cdl`) e a logo da CDL.

## O que já faz

| Área | Funções |
|---|---|
| **Funil (quadro)** | Vários funis; colunas por etapa com quantidade e soma de valores; arrastar e soltar cartões entre etapas e dentro da coluna; "Adicionar" rápido no topo da coluna; busca por lead, contato, telefone ou e-mail; filtro por responsável; prazo da próxima tarefa no cartão (atrasada em vermelho, hoje em laranja). |
| **Lead** | Barra de etapas clicável; valor, responsável, tags; campos personalizados (lista, múltipla escolha, data, texto, UTM…); contatos vinculados (com link direto para o WhatsApp); empresa; notas; tarefas; histórico automático (criação, mudança de etapa, responsável, tarefas); motivo de perda. |
| **Contatos e empresas** | Lista com busca e paginação; ficha com campos fixos e personalizados; leads vinculados. |
| **Tarefas** | Em aberto, atrasadas, para hoje, concluídas; concluir com resultado. |
| **Painel (gestores)** | Vendas ganhas, novos leads, conversão, valor em negociação, leads por etapa, motivos de perda e desempenho por responsável, por período. |
| **Configurações (administrador)** | Funis e etapas (criar, renomear, cor, reordenar arrastando, arquivar); campos personalizados; usuários e perfis; motivos de perda; importação da Kommo. |

**Perfis:** Vendedor(a) vê só os próprios leads · Gestor vê todos os leads e o painel · Administrador também configura o sistema.

## Estrutura

```
src/
  CrmCDL.Domain          entidades (Lead, Funil, Etapa, Contato, Empresa, Tarefa, Atividade, Campos…)
  CrmCDL.Application     regras de negócio (LeadService, ConfiguracaoService)
  CrmCDL.Infrastructure  banco (AppDbContext, migrações) e importador da Kommo
  CrmCDL.Web             telas (MVC + Razor), CSS e JS sem bibliotecas
banco/criar-banco.sql    cria o banco CrmCDL e o usuário próprio
```

## Banco de dados

O CRM usa **o mesmo servidor MySQL do site, mas um banco separado (`CrmCDL`) com usuário próprio**.
Esse usuário só tem permissão no banco `CrmCDL`, então o CRM não consegue ler nem alterar nada do site.

1. Abra `banco/criar-banco.sql`, troque `TROQUE_ESTA_SENHA` por uma senha forte e rode no MySQL com um usuário administrador.
2. As tabelas são criadas sozinhas na primeira vez que o sistema sobe (migrações do Entity Framework).

## Configuração

Nada de senha no código: tudo vem de variáveis de ambiente (veja `.env.exemplo`).

| Variável | Para quê |
|---|---|
| `ConnectionStrings__CrmCDL` | Conexão com o banco `CrmCDL` (usuário `crmcdl`). |
| `Admin__Email`, `Admin__Senha`, `Admin__Nome` | Cria o primeiro administrador quando o banco está vazio. Depois pode remover. |
| `Kommo__BaseUrl`, `Kommo__Token` | Importação da Kommo (token de longa duração da integração privada). |

## Rodar localmente

```bash
cd src/CrmCDL.Web
export ConnectionStrings__CrmCDL="Server=127.0.0.1;Database=CrmCDL;User=crmcdl;Password=..."
export Admin__Email="seu@email" Admin__Senha="umaSenhaForte"
dotnet run
```

Abra http://localhost:5000, entre com o administrador e vá em **Configurações → Importar da Kommo**.

## Importação da Kommo

Traz funis, etapas, campos personalizados (com as opções), motivos de perda e tags. Só lê a Kommo.
Pode ser executada de novo: o que já foi importado é atualizado pelo id da Kommo, sem duplicar.
Funis com nome começando por "Teste" ficam de fora (ajustável em `Kommo:IgnorarFunisQueComecamCom`).
Etapas digitadas com Caps Lock ("aGUARDANDO") são corrigidas ("Aguardando") e etapas repetidas são unificadas.

## Nova migração (quando mudar as entidades)

```bash
dotnet tool install --global dotnet-ef --version 8.0.10
cd src
ConnectionStrings__CrmCDL="Server=localhost;Database=CrmCDL;User=x;Password=x" \
  dotnet ef migrations add NomeDaMudanca -p CrmCDL.Infrastructure -s CrmCDL.Web -o Migrations
```
