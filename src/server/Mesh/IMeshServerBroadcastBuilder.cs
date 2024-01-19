namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// This is used by the ServerMeshMonitor to build the status of the current node.
    /// </summary>
    public interface IMeshServerBroadcastBuilder<TStatus> where TStatus: MeshServerBroadcastData, new()
    {
        /// <summary>
        /// Build the current node's status
        /// </summary>
        /// <param name="meshMonitor">The current mesh monitor</param>
        /// <returns>The Mesh Node's status.</returns>
        TStatus Build(BaseMeshMonitor<TStatus> meshMonitor);
    }
}
