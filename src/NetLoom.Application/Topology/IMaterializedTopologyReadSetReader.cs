namespace NetLoom.Application.Topology
{
    public interface IMaterializedTopologyReadSetReader
    {
        MaterializedTopologyReadSet Read(
            string stpInstanceId);
    }
}
