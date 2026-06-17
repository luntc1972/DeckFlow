namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Facade contract that aggregates the focused Content KB orchestration slices by inheritance.
/// </summary>
public interface IContentKbOrchestrator :
    IHarvestOrchestrator,
    IDistillOrchestrator,
    IContentMaintenanceOrchestrator,
    IContentSourceManager,
    IContentIndexExporter
{
}
