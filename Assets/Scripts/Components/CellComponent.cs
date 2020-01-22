using Unity.Entities;
using Unity.Mathematics;

public struct CellComponent : IComponentData
{
    public bool IsAlive;
    public bool ChangeTo;
}