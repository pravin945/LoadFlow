namespace LoadFlow.Api.Authorization;

public static class PermissionPolicies
{
    public const string PolicyPrefix = "Permission:";

    // Attribute arguments must be compile-time constants. Keep these names
    // centralized so controllers do not build policy strings with a method call.
    public const string LoadCreate = PolicyPrefix + Models.PermissionCatalog.LoadCreate;
    public const string LoadAssignCarrier = PolicyPrefix + Models.PermissionCatalog.LoadAssignCarrier;
    public const string LoadOverrideCompliance = PolicyPrefix + Models.PermissionCatalog.LoadOverrideCompliance;
    public const string RateConfirm = PolicyPrefix + Models.PermissionCatalog.RateConfirm;
    public const string LoadUpdateStatus = PolicyPrefix + Models.PermissionCatalog.LoadUpdateStatus;
    public const string StaffManage = PolicyPrefix + Models.PermissionCatalog.StaffManage;
    public const string PodUpload = PolicyPrefix + Models.PermissionCatalog.PodUpload;
    public const string LoadView = PolicyPrefix + Models.PermissionCatalog.LoadView;
    public const string ComplianceManage = PolicyPrefix + Models.PermissionCatalog.ComplianceManage;

    public static string For(string permission) => PolicyPrefix + permission;

    public static readonly string[] AllPolicyNames =
        Models.PermissionCatalog.All.Select(For).ToArray();
}

public static class ClaimTypes
{
    public const string UserId = "uid";
    public const string AccountType = "account_type";
    public const string OrganizationId = "org_id";
    public const string IsOrgAdmin = "is_org_admin";
    public const string Permissions = "permissions";
}
