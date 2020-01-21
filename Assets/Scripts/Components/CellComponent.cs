using Unity.Entities;

public struct CellComponent : IComponentData
{
    public bool IsAlive;
    public bool ChangeTo;
}