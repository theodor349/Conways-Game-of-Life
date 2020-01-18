﻿using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using UnityEditor;
using UnityEngine;

public class ChangeCellColorSystem : JobComponentSystem
{
    private Material deadMaterial;
    private Material aliveMaterial;
    private Mesh planeMesh;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        // Material
        deadMaterial = new Material(Shader.Find("Standard")) {color = Color.black};
        aliveMaterial = new Material(Shader.Find("Standard")) {color = Color.white};
        // Mesh
        var p = ObjectFactory.CreatePrimitive(PrimitiveType.Plane);
        planeMesh = p.GetComponent<MeshFilter>().mesh;
        Object.Destroy(p);
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var manager = World.DefaultGameObjectInjectionWorld.EntityManager;
        Entities.WithStructuralChanges().ForEach((Entity entity, ref CellComponent cell, in ChangeCellStateComponent change) =>
        {
            cell.IsAlive = change.ChangeTo;
            manager.RemoveComponent(entity, typeof(ChangeCellStateComponent));
            manager.RemoveComponent(entity, typeof(RenderMesh));
            
            manager.AddSharedComponentData(entity, new RenderMesh()
            {
                material = cell.IsAlive ? aliveMaterial : deadMaterial,
                mesh = planeMesh
            });
        }).Run();

        return inputDeps;
    }
}
