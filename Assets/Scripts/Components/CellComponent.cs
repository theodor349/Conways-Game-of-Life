using Unity.Entities;

public struct CellComponent : IComponentData
{
    public bool IsAlive;
}

public struct ChangeCellStateComponent : IComponentData
{
    public bool ChangeTo;
}