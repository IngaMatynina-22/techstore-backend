namespace TechStore.Domain.Common;

public interface ISoftDeactivatable
{
    bool IsActive { get; set; }
}
