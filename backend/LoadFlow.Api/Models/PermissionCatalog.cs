namespace LoadFlow.Api.Models;

/// <summary>
/// Fixed permission catalog. Code checks permissions, never role names.
/// </summary>
public static class PermissionCatalog
{
    public const string LoadCreate = "load.create";
    public const string LoadAssignCarrier = "load.assign_carrier";
    public const string LoadOverrideCompliance = "load.override_compliance_flag";
    public const string RateConfirm = "rate.confirm";
    public const string LoadUpdateStatus = "load.update_status";
    public const string StaffManage = "staff.manage";
    public const string PodUpload = "pod.upload";
    public const string LoadView = "load.view";
    public const string ComplianceManage = "compliance.manage";

    public static readonly IReadOnlyList<string> All =
    [
        LoadCreate,
        LoadAssignCarrier,
        LoadOverrideCompliance,
        RateConfirm,
        LoadUpdateStatus,
        StaffManage,
        PodUpload,
        LoadView,
        ComplianceManage
    ];

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [LoadCreate] = "Create and edit broker loads",
        [LoadAssignCarrier] = "Assign carriers to loads",
        [LoadOverrideCompliance] = "Override compliance flags blocking progression",
        [RateConfirm] = "Confirm negotiated rates with carriers",
        [LoadUpdateStatus] = "Update load/shipment status (includes accept/decline)",
        [StaffManage] = "Manage staff accounts and custom roles",
        [PodUpload] = "Upload proof of delivery documents",
        [LoadView] = "View loads within org/object scope",
        [ComplianceManage] = "Manage carrier compliance records"
    };
}
