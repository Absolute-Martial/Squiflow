using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace SquiFlow.IdentityAccess.Postgres.Migrations;

[DbContext(typeof(IdentityAccessDbContext))]
partial class IdentityAccessDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        IdentityAccessModel.Build(modelBuilder);
    }
}

internal static class IdentityAccessModel
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.8");
        modelBuilder.HasDefaultSchema("identity_access");

        modelBuilder.Entity("SquiFlow.IdentityAccess.Postgres.AccountRow", entity =>
        {
            entity.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("id");
            entity.Property<short>("Availability")
                .HasColumnType("smallint")
                .HasColumnName("availability");
            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property<DateTimeOffset?>("DisabledAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("disabled_at");
            entity.HasKey("Id").HasName("pk_accounts");
            entity.ToTable("accounts", "identity_access", table =>
                table.HasCheckConstraint("ck_accounts_availability", "availability IN (1, 2)"));
        });

        modelBuilder.Entity("SquiFlow.IdentityAccess.Postgres.ExternalIdentityBindingRow", entity =>
        {
            entity.Property<string>("Issuer")
                .HasMaxLength(255)
                .HasColumnType("character varying(255)")
                .HasColumnName("issuer");
            entity.Property<string>("Subject")
                .HasMaxLength(255)
                .HasColumnType("character varying(255)")
                .HasColumnName("subject");
            entity.Property<Guid>("AccountId")
                .HasColumnType("uuid")
                .HasColumnName("account_id");
            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.HasKey("Issuer", "Subject").HasName("pk_external_identity_bindings");
            entity.HasIndex("AccountId").HasDatabaseName("ix_external_identity_bindings_account_id");
            entity.ToTable("external_identity_bindings", "identity_access", table =>
            {
                table.HasCheckConstraint(
                    "ck_external_identity_bindings_issuer_not_blank",
                    "btrim(issuer) <> ''");
                table.HasCheckConstraint(
                    "ck_external_identity_bindings_subject_not_blank",
                    "btrim(subject) <> ''");
            });
        });

        modelBuilder.Entity("SquiFlow.IdentityAccess.Postgres.ExternalIdentityBindingRow", entity =>
        {
            entity.HasOne("SquiFlow.IdentityAccess.Postgres.AccountRow", null)
                .WithMany()
                .HasForeignKey("AccountId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_external_identity_bindings_accounts_account_id");
        });
    }
}
