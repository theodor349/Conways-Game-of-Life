using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

public class ChangeCellStateSystem : JobComponentSystem
{
    private bool isRunning;
    
    struct CellStateJob : IJobForEach<CellComponent, Translation>
    {
        public void Execute(ref CellComponent cell, ref Translation transform)
        {
            cell.IsAlive = false;
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (Input.GetKeyDown(KeyCode.Space))
            isRunning = !isRunning;

        if (!isRunning) 
            return inputDeps;
        
        var job = new CellStateJob()
        {
            
        };
        return job.Schedule(this, inputDeps);

    }
}
