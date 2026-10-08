namespace Application.Customers.Postgres;

internal static class CustomerSql
{
    internal static readonly string SetTenant = Load(nameof(SetTenant));
    internal static readonly string InsertOrganization = Load(nameof(InsertOrganization));
    internal static readonly string InsertOrganizationReceipt = Load(nameof(InsertOrganizationReceipt));
    internal static readonly string InsertProgram = Load(nameof(InsertProgram));
    internal static readonly string InsertProgramReceipt = Load(nameof(InsertProgramReceipt));
    internal static readonly string FindOrganization = Load(nameof(FindOrganization));
    internal static readonly string FindProgram = Load(nameof(FindProgram));
    internal static readonly string FindOrganizationReceipt = Load(nameof(FindOrganizationReceipt));
    internal static readonly string FindProgramReceipt = Load(nameof(FindProgramReceipt));
    internal static readonly string ListOrganizations = Load(nameof(ListOrganizations));
    internal static readonly string ListPrograms = Load(nameof(ListPrograms));
    internal static readonly string InsertIndividual = Load(nameof(InsertIndividual));
    internal static readonly string InsertIndividualWithSignals = Load(nameof(InsertIndividualWithSignals));
    internal static readonly string FindIndividual = Load(nameof(FindIndividual));
    internal static readonly string UpdateIndividualContact = Load(nameof(UpdateIndividualContact));
    internal static readonly string UpdateIndividualAvailability = Load(nameof(UpdateIndividualAvailability));
    internal static readonly string InsertIndividualReceipt = Load(nameof(InsertIndividualReceipt));
    internal static readonly string FindIndividualReceipt = Load(nameof(FindIndividualReceipt));
    internal static readonly string InsertRepresentative = Load(nameof(InsertRepresentative));
    internal static readonly string FindRepresentative = Load(nameof(FindRepresentative));
    internal static readonly string FindActiveRepresentative = Load(nameof(FindActiveRepresentative));
    internal static readonly string UpdateRepresentativeAvailability = Load(nameof(UpdateRepresentativeAvailability));
    internal static readonly string InsertRepresentativeReceipt = Load(nameof(InsertRepresentativeReceipt));
    internal static readonly string FindRepresentativeReceipt = Load(nameof(FindRepresentativeReceipt));
    internal static readonly string FindPotentialDuplicates = Load(nameof(FindPotentialDuplicates));
    internal static readonly string FindDuplicateReceipt = Load(nameof(FindDuplicateReceipt));
    internal static readonly string InsertDuplicateReceipt = Load(nameof(InsertDuplicateReceipt));
    internal static readonly string UpsertDuplicateCase = Load(nameof(UpsertDuplicateCase));
    internal static readonly string InsertCustomerRedirect = Load(nameof(InsertCustomerRedirect));
    internal static readonly string FindImport = Load(nameof(FindImport));
    internal static readonly string FindImportReceipt = Load(nameof(FindImportReceipt));
    internal static readonly string InsertImport = Load(nameof(InsertImport));
    internal static readonly string InsertImportRowsBatch = Load(nameof(InsertImportRowsBatch));
    internal static readonly string FindImportWork = Load(nameof(FindImportWork));
    internal static readonly string InsertImportWork = Load(nameof(InsertImportWork));
    internal static readonly string FindImportRows = Load(nameof(FindImportRows));
    internal static readonly string LockDuplicateCustomers = Load(nameof(LockDuplicateCustomers));
    internal static readonly string UpdateDuplicateCase = Load(nameof(UpdateDuplicateCase));
    internal static readonly string UpdateCustomerRedirect = Load(nameof(UpdateCustomerRedirect));
    internal static readonly string LockCustomerCanonicalization = Load(nameof(LockCustomerCanonicalization));
    internal static readonly string LockCustomerConsolidation = Load(nameof(LockCustomerConsolidation));
    internal static readonly string FindCurrentCustomer = Load(nameof(FindCurrentCustomer));
    internal static readonly string RedirectRepresentatives = Load(nameof(RedirectRepresentatives));
    internal static readonly string ReadDuplicateResolutions = Load(nameof(ReadDuplicateResolutions));
    internal static readonly string ReadPairDuplicateEvidence = Load(nameof(ReadPairDuplicateEvidence));
    internal static readonly string LockImport = Load(nameof(LockImport));
    internal static readonly string ValidateImportDuplicates = Load(nameof(ValidateImportDuplicates));
    internal static readonly string HasUnplannedImportDuplicate = Load(nameof(HasUnplannedImportDuplicate));
    internal static readonly string SetImportDecisionsBatch = Load(nameof(SetImportDecisionsBatch));
    internal static readonly string FindCanonicalImportTarget = Load(nameof(FindCanonicalImportTarget));
    internal static readonly string ReadImportSummary = Load(nameof(ReadImportSummary));
    internal static readonly string ClaimImport = Load(nameof(ClaimImport));
    internal static readonly string LockImportClaim = Load(nameof(LockImportClaim));
    internal static readonly string NextImportRow = Load(nameof(NextImportRow));
    internal static readonly string CompleteImportRow = Load(nameof(CompleteImportRow));
    internal static readonly string ReleaseImportClaim = Load(nameof(ReleaseImportClaim));
    internal static readonly string ImportSavepoint = Load(nameof(ImportSavepoint));
    internal static readonly string ImportRollbackSavepoint = Load(nameof(ImportRollbackSavepoint));
    internal static readonly string HasCurrentImportDuplicate = Load(nameof(HasCurrentImportDuplicate));
    internal static readonly string ResumeImportWork = Load(nameof(ResumeImportWork));
    internal static readonly string CompleteEmptyImport = Load(nameof(CompleteEmptyImport));
    internal static readonly string DiscoverImportTenants = Load(nameof(DiscoverImportTenants));
    internal static readonly string FindImportSource = Load(nameof(FindImportSource));
    internal static readonly string FindImportSourceByImport = Load(nameof(FindImportSourceByImport));
    internal static readonly string FindImportSourceReservation = Load(nameof(FindImportSourceReservation));
    internal static readonly string FindImportSourceReservationById = Load(nameof(FindImportSourceReservationById));
    internal static readonly string InsertImportSource = Load(nameof(InsertImportSource));
    internal static readonly string InsertImportSourceReservation = Load(nameof(InsertImportSourceReservation));
    internal static readonly string EnsureImportSourceUsage = Load(nameof(EnsureImportSourceUsage));
    internal static readonly string LockImportSourceUsage = Load(nameof(LockImportSourceUsage));
    internal static readonly string ReserveImportSourceUsage = Load(nameof(ReserveImportSourceUsage));
    internal static readonly string LockImportSource = Load(nameof(LockImportSource));
    internal static readonly string MarkImportSourceAvailable = Load(nameof(MarkImportSourceAvailable));
    internal static readonly string CommitImportSourceReservation = Load(nameof(CommitImportSourceReservation));
    internal static readonly string CommitImportSourceUsage = Load(nameof(CommitImportSourceUsage));
    internal static readonly string MarkImportSourceFailure = Load(nameof(MarkImportSourceFailure));
    internal static readonly string ReleaseImportSourceReservation = Load(nameof(ReleaseImportSourceReservation));
    internal static readonly string ReleaseImportSourceUsage = Load(nameof(ReleaseImportSourceUsage));
    internal static readonly string MarkImportSourceReservationUnknown = Load(nameof(MarkImportSourceReservationUnknown));
    internal static readonly string ClaimExpiredImportSourceRetirement = Load(nameof(ClaimExpiredImportSourceRetirement));
    internal static readonly string CompleteImportSourceRetirement = Load(nameof(CompleteImportSourceRetirement));
    internal static readonly string UpdateImportSourceKey = Load(nameof(UpdateImportSourceKey));

    private static string Load(string name)
    {
        var resourceName = $"{typeof(CustomerSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(CustomerSql).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded Customers SQL resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
