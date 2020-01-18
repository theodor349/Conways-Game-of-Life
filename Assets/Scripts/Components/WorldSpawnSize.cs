using Unity.Entities;

[GenerateAuthoringComponent]
public struct WorldSpawnSize : IComponentData
{
    public int Width;
    public int Height;
}