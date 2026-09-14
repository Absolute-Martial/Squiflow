using Avalonia;
using Avalonia.Controls;
using SquiFlow.Customers.Domain;

namespace SquiFlow.Customers.Workstation;

public sealed class CustomerWorkspaceView : UserControl
{
    public CustomerWorkspaceView()
    {
        var heading = new TextBlock
        {
            Text = "Customers",
            FontSize = 24
        };

        var explanation = new TextBlock
        {
            Text = "Phase 0 proves shared deterministic business meaning only. No customer is persisted or made authoritative here.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var nameInput = new TextBox
        {
            Watermark = "Customer name"
        };

        var result = new TextBlock
        {
            Text = "Enter a name to exercise the shared CustomerName invariant."
        };

        var validateButton = new Button
        {
            Content = "Validate locally"
        };

        validateButton.Click += (_, _) =>
        {
            try
            {
                var customerName = new CustomerName(nameInput.Text ?? string.Empty);
                result.Text = $"Valid deterministic value: {customerName.Value}";
            }
            catch (ArgumentException exception)
            {
                result.Text = exception.Message;
            }
        };

        var content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 12
        };
        content.Children.Add(heading);
        content.Children.Add(explanation);
        content.Children.Add(nameInput);
        content.Children.Add(validateButton);
        content.Children.Add(result);

        Content = content;
    }
}
