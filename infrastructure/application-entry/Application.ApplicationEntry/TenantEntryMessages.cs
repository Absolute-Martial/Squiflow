namespace Application.ApplicationEntry;

public static class TenantEntryMessages
{
    public static string For(TenantEntryFailure failure) => failure switch
    {
        TenantEntryFailure.SignInRequired => "Your session has ended. Sign in again to continue.",
        TenantEntryFailure.AccountUnavailable => "This identity cannot access an active application account. Contact support.",
        TenantEntryFailure.TenantDenied => "You cannot enter this workspace. Choose another tenant or contact support.",
        TenantEntryFailure.TimedOut => "The access check took too long. Try again.",
        TenantEntryFailure.InvalidResponse => "The service returned an invalid access response. Try again or contact support.",
        _ => "The access check is unavailable. Try again.",
    };
}
