using System.Windows.Input;
using Application.ApplicationEntry;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Application.Workstation;

public sealed class ConnectionViewModel : ObservableObject
{
    private readonly ApplicationEntryClient client;
    private string address;
    private string applicationName = "Application connection";
    private string legalName = "";
    private string heading = "Connection not checked";
    private string message = "Enter your application service address and check the connection.";

    public ConnectionViewModel(ApplicationEntryClient client, string address = "")
    {
        this.client = client;
        this.address = address;
        ConnectCommand = new AsyncRelayCommand(CheckAsync);
        CancelCommand = ConnectCommand.CreateCancelCommand();
    }

    public string Address { get => address; set => SetProperty(ref address, value); }
    public string ApplicationName { get => applicationName; private set => SetProperty(ref applicationName, value); }
    public string LegalName { get => legalName; private set => SetProperty(ref legalName, value); }
    public string Heading { get => heading; private set => SetProperty(ref heading, value); }
    public string Message { get => message; private set => SetProperty(ref message, value); }
    public IAsyncRelayCommand ConnectCommand { get; }
    public ICommand CancelCommand { get; }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        ApplicationName = "Application connection";
        LegalName = string.Empty;
        if (!ServiceAddress.TryParse(Address, out var serviceAddress))
        {
            Heading = "Check the service address";
            Message = "Use an HTTPS address without a path, such as https://app.example.com. HTTP is allowed only on localhost.";
            return;
        }

        Heading = "Checking connection…";
        Message = "Contacting your application service.";
        try
        {
            var result = await client.CheckAsync(serviceAddress!, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Heading = result.Heading;
            Message = result.Message;
            ApplicationName = result.Application?.Brand.DisplayName ?? "Application connection";
            LegalName = result.Application?.Brand.LegalName ?? string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Heading = "Check canceled";
            Message = "The connection check was canceled. Try again when you are ready.";
        }
    }
}
