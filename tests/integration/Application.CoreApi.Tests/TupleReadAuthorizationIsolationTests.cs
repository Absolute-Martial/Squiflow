using Application.CoreApi.Authorization;
using Application.Tenancy;
using Microsoft.Extensions.Configuration;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Exceptions;
using OpenFga.Sdk.Model;
using Xunit;

namespace Application.CoreApi.Tests;

// Granting a direct permission and revoking one need different provider authorizations, and
// only one of them needs to read tuples. These cases pin that asymmetry with a client that
// refuses to read, because the real-server suites authenticate with CredentialMethod None
// and therefore cannot observe which SDK calls a proposal makes.
public sealed class TupleReadAuthorizationIsolationTests
{
    [Fact]
    public async Task GrantAppliesWithoutAnyTupleReadAuthorization()
    {
        var client = new WriteAndCheckOnlyClient();
        var provider = new OpenFgaTenantAuthorizationAdministrationProvider(client, Configuration());

        var result = await provider.EnsureAsync(
            Proposal(TenantAuthorizationProposalKind.GrantPermission),
            [],
            CancellationToken.None);

        Assert.Equal(TenantAuthorizationProviderOutcome.Applied, result.Outcome);
        Assert.Equal(0, client.TupleReadCount);
    }

    [Fact]
    public async Task RevokeReportsADeniedCredentialDistinctlyFromATransientOutage()
    {
        var client = new WriteAndCheckOnlyClient();
        var provider = new OpenFgaTenantAuthorizationAdministrationProvider(client, Configuration());

        var result = await provider.EnsureAsync(
            Proposal(TenantAuthorizationProposalKind.RevokePermission),
            [],
            CancellationToken.None);

        // Revoking must prove the direct tuple is gone, which needs tuple read. A credential
        // without it can never resolve this proposal, so the outcome must say that rather than
        // report a retryable outage that would burn the attempt budget for ever.
        Assert.Equal(TenantAuthorizationProviderOutcome.Uncertain, result.Outcome);
        Assert.Equal("provider_denied_credential", result.FailureCode);
        Assert.Equal(1, client.TupleReadCount);
    }

    private static OpenFgaAuthorizationConfiguration Configuration() =>
        OpenFgaAuthorizationConfiguration.From(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:OpenFga:ApiUrl"] = "http://127.0.0.1:8080",
                ["Authorization:OpenFga:StoreId"] = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                ["Authorization:OpenFga:AuthorizationModelId"] = "01ARZ3NDEKTSV4RRFFQ69G5FAW",
                ["Authorization:OpenFga:RequestTimeoutSeconds"] = "5",
                ["Authorization:OpenFga:MaximumRetries"] = "0",
                ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
                ["Authorization:OpenFga:CredentialMethod"] = "None",
            })
            .Build());

    private static TenantAuthorizationProposal Proposal(TenantAuthorizationProposalKind kind) =>
        new(
            Guid.NewGuid(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "tuple-read-authorization-isolation",
            new string('A', 64),
            kind,
            TenantAuthorizationProposalStatus.Pending,
            1,
            null,
            Guid.CreateVersion7(),
            "orders.view",
            null,
            null,
            null,
            [],
            [],
            0,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

#nullable disable
    // Models a credential that may write tuples and check permissions but is not authorized to
    // read them, which is the deployment shape that previously wedged every direct grant.
    private sealed class WriteAndCheckOnlyClient : IOpenFgaClient
    {
        internal int TupleReadCount { get; private set; }

        public Task<ClientWriteResponse> Write(
            ClientWriteRequest body, IClientWriteOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(new ClientWriteResponse());

        public Task<CheckResponse> Check(
            IClientCheckRequest body, IClientCheckOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(new CheckResponse { Allowed = true });

        public Task<ReadResponse> Read(
            ClientReadRequest body, IClientReadOptions options, CancellationToken cancellationToken)
        {
            TupleReadCount++;
            throw new FgaApiError(
                System.Net.HttpStatusCode.Forbidden,
                "the credential is not authorized to read tuples",
                null,
                shouldRetry: false);
        }

        public string StoreId { get; set; } = string.Empty;

        public string AuthorizationModelId { get; set; } = string.Empty;

        public Task<ListStoresResponse> ListStores(IClientListStoresRequest body, IClientListStoresOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CreateStoreResponse> CreateStore(ClientCreateStoreRequest body, IClientRequestOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GetStoreResponse> GetStore(IClientRequestOptionsWithStoreId options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteStore(IClientRequestOptionsWithStoreId options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ReadAuthorizationModelsResponse> ReadAuthorizationModels(IClientReadAuthorizationModelsOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WriteAuthorizationModelResponse> WriteAuthorizationModel(ClientWriteAuthorizationModelRequest body, IClientRequestOptionsWithStoreId options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ReadAuthorizationModelResponse> ReadAuthorizationModel(IClientReadAuthorizationModelOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ReadAuthorizationModelResponse> ReadLatestAuthorizationModel(IClientRequestOptionsWithAuthZModelId options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ReadChangesResponse> ReadChanges(ClientReadChangesRequest body, ClientReadChangesOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ClientWriteResponse> WriteTuples(List<ClientTupleKey> body, IClientWriteOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ClientWriteResponse> DeleteTuples(List<ClientTupleKeyWithoutCondition> body, IClientWriteOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ClientBatchCheckResponse> BatchCheck(ClientBatchCheckRequest body, IClientBatchCheckOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ClientBatchCheckClientResponse> ClientBatchCheck(List<ClientCheckRequest> body, IClientBatchCheckClientOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ExpandResponse> Expand(IClientExpandRequest body, IClientExpandOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ListObjectsResponse> ListObjects(IClientListObjectsRequest body, IClientListObjectsOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ListRelationsResponse> ListRelations(IClientListRelationsRequest body, IClientListRelationsOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ListUsersResponse> ListUsers(IClientListUsersRequest body, IClientListUsersOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ReadAssertionsResponse> ReadAssertions(IClientReadAssertionsOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task WriteAssertions(List<ClientAssertion> body, IClientWriteAssertionsOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
#nullable restore
