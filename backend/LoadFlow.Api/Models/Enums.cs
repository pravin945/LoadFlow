namespace LoadFlow.Api.Models;

public enum AccountType
{
    Broker = 1,
    Carrier = 2,
    Shipper = 3
}

public enum LoadStatus
{
    Posted = 1,
    CarrierAssigned = 2,
    RateConfirmed = 3,
    Dispatched = 4,
    InTransit = 5,
    Delivered = 6,
    PodVerified = 7,
    InvoicedClosed = 8
}

public enum AuthorityStatus
{
    Active = 1,
    Inactive = 2,
    Pending = 3
}

public enum ComplianceFlagStatus
{
    None = 0,
    Flagged = 1,
    Overridden = 2
}
