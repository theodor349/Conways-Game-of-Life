using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public class SpawnSystem : ComponentSystem
{
    public static int2 WorldSize;
    
    private EntityArchetype cellArchetype;
    private Material deadMaterial;
    private Mesh planeMesh;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        // EntityArchType
        cellArchetype = World.DefaultGameObjectInjectionWorld.EntityManager.CreateArchetype(
            typeof(LocalToWorld),
            typeof(RenderMesh),
            typeof(Scale),
            typeof(Translation),
            typeof(CellComponent)
        );
        
        // Material
        deadMaterial = new Material(Shader.Find("Standard")) {color = Color.black};
        // Mesh
        var p = GameObject.CreatePrimitive(PrimitiveType.Plane);
        planeMesh = p.GetComponent<MeshFilter>().mesh;
        Object.Destroy(p);
    }

    protected override void OnUpdate()
    {
        Entities.ForEach((Entity entity, ref WorldSpawnSize worldSize) =>
        {
            ChangeCellStateSystem.TickRate = worldSize.TickRate;
            
            WorldSize = new int2(worldSize.Width, worldSize.Height);
            
            for (int x = 0; x < worldSize.Width; x++)
            {
                for (int y = 0; y < worldSize.Height; y++)
                {
                    var c = PostUpdateCommands.CreateEntity(cellArchetype);
                    PostUpdateCommands.SetComponent(c, new LocalToWorld());
                    PostUpdateCommands.SetSharedComponent(c, new RenderMesh(){material = deadMaterial, mesh = planeMesh});
                    PostUpdateCommands.SetComponent(c, new Translation(){Value = new float3(x, 0, y)});
                    PostUpdateCommands.SetComponent(c, new CellComponent(){});
                    PostUpdateCommands.SetComponent(c, new Scale(){Value = 0.1f});
                }
            }
            
            PostUpdateCommands.DestroyEntity(entity);
        });
    }

}
