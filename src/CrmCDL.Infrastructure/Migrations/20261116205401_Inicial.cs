using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmCDL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Campos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Entidade = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    KommoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campos", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Funis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Principal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Ativo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    KommoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Funis", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MotivosPerda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    KommoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivosPerda", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cor = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KommoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SenhaHash = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Perfil = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    KommoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CampoOpcoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CampoId = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    KommoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampoOpcoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampoOpcoes_Campos_CampoId",
                        column: x => x.CampoId,
                        principalTable: "Campos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ValoresCampo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CampoId = table.Column<int>(type: "int", nullable: false),
                    Entidade = table.Column<int>(type: "int", nullable: false),
                    RegistroId = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoresCampo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValoresCampo_Campos_CampoId",
                        column: x => x.CampoId,
                        principalTable: "Campos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Etapas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FunilId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cor = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    KommoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Etapas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Etapas_Funis_FunilId",
                        column: x => x.FunilId,
                        principalTable: "Funis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cnpj = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Telefone = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Site = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Endereco = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponsavelId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    KommoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Empresas_Usuarios_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Contatos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cargo = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Telefone = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Celular = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cpf = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AceitouTermos = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OptInMarketing = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    ResponsavelId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    KommoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contatos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contatos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Contatos_Usuarios_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Titulo = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Valor = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    FunilId = table.Column<int>(type: "int", nullable: false),
                    EtapaId = table.Column<int>(type: "int", nullable: false),
                    ResponsavelId = table.Column<int>(type: "int", nullable: true),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    MotivoPerdaId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Posicao = table.Column<double>(type: "double", nullable: false),
                    KommoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_Etapas_EtapaId",
                        column: x => x.EtapaId,
                        principalTable: "Etapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leads_Funis_FunilId",
                        column: x => x.FunilId,
                        principalTable: "Funis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leads_MotivosPerda_MotivoPerdaId",
                        column: x => x.MotivoPerdaId,
                        principalTable: "MotivosPerda",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_Usuarios_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Atividades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Atividades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Atividades_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Atividades_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeadContatos",
                columns: table => new
                {
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    ContatoId = table.Column<int>(type: "int", nullable: false),
                    Principal = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadContatos", x => new { x.LeadId, x.ContatoId });
                    table.ForeignKey(
                        name: "FK_LeadContatos_Contatos_ContatoId",
                        column: x => x.ContatoId,
                        principalTable: "Contatos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadContatos_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeadTags",
                columns: table => new
                {
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    TagId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadTags", x => new { x.LeadId, x.TagId });
                    table.ForeignKey(
                        name: "FK_LeadTags_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Tarefas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LeadId = table.Column<int>(type: "int", nullable: true),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Prazo = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ResponsavelId = table.Column<int>(type: "int", nullable: true),
                    Concluida = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ConcluidaEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Resultado = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CriadoPorId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarefas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tarefas_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tarefas_Usuarios_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Atividades_LeadId_CriadoEm",
                table: "Atividades",
                columns: new[] { "LeadId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Atividades_UsuarioId",
                table: "Atividades",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_CampoOpcoes_CampoId",
                table: "CampoOpcoes",
                column: "CampoId");

            migrationBuilder.CreateIndex(
                name: "IX_CampoOpcoes_KommoId",
                table: "CampoOpcoes",
                column: "KommoId");

            migrationBuilder.CreateIndex(
                name: "IX_Campos_KommoId",
                table: "Campos",
                column: "KommoId");

            migrationBuilder.CreateIndex(
                name: "IX_Contatos_EmpresaId",
                table: "Contatos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Contatos_KommoId",
                table: "Contatos",
                column: "KommoId");

            migrationBuilder.CreateIndex(
                name: "IX_Contatos_Nome",
                table: "Contatos",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_Contatos_ResponsavelId",
                table: "Contatos",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_KommoId",
                table: "Empresas",
                column: "KommoId");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Nome",
                table: "Empresas",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_ResponsavelId",
                table: "Empresas",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_Etapas_FunilId",
                table: "Etapas",
                column: "FunilId");

            migrationBuilder.CreateIndex(
                name: "IX_Etapas_KommoId",
                table: "Etapas",
                column: "KommoId");

            migrationBuilder.CreateIndex(
                name: "IX_Funis_KommoId",
                table: "Funis",
                column: "KommoId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadContatos_ContatoId",
                table: "LeadContatos",
                column: "ContatoId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_EmpresaId",
                table: "Leads",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_EtapaId",
                table: "Leads",
                column: "EtapaId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_FunilId_EtapaId_Posicao",
                table: "Leads",
                columns: new[] { "FunilId", "EtapaId", "Posicao" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_KommoId",
                table: "Leads",
                column: "KommoId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_MotivoPerdaId",
                table: "Leads",
                column: "MotivoPerdaId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ResponsavelId",
                table: "Leads",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTags_TagId",
                table: "LeadTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Nome",
                table: "Tags",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_LeadId",
                table: "Tarefas",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_ResponsavelId_Concluida_Prazo",
                table: "Tarefas",
                columns: new[] { "ResponsavelId", "Concluida", "Prazo" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValoresCampo_CampoId",
                table: "ValoresCampo",
                column: "CampoId");

            migrationBuilder.CreateIndex(
                name: "IX_ValoresCampo_Entidade_RegistroId_CampoId",
                table: "ValoresCampo",
                columns: new[] { "Entidade", "RegistroId", "CampoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Atividades");

            migrationBuilder.DropTable(
                name: "CampoOpcoes");

            migrationBuilder.DropTable(
                name: "LeadContatos");

            migrationBuilder.DropTable(
                name: "LeadTags");

            migrationBuilder.DropTable(
                name: "Tarefas");

            migrationBuilder.DropTable(
                name: "ValoresCampo");

            migrationBuilder.DropTable(
                name: "Contatos");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "Campos");

            migrationBuilder.DropTable(
                name: "Empresas");

            migrationBuilder.DropTable(
                name: "Etapas");

            migrationBuilder.DropTable(
                name: "MotivosPerda");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Funis");
        }
    }
}
