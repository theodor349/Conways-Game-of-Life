using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class ChangeCellStateSystem : JobComponentSystem
{
    public static float tickRate = 1f;
    
    private bool isRunning;
    private float timePassed = 0f;

    private struct CellStateJob : IJobForEach<CellComponent, Translation>
    {
        [ReadOnly]
        public NativeArray<CellComponent> OtherCells;
        public int Width;
        public int Height;
        
        public void Execute(ref CellComponent cell, ref Translation pos)
        {
            int aliveNeighbours = 0;
            // N 
            aliveNeighbours += GetStateOfCell(pos.Value.x, pos.Value.z + 1) ? 1 : 0;
            // NE
            aliveNeighbours += GetStateOfCell(pos.Value.x + 1, pos.Value.z + 1) ? 1 : 0;
            // E
            aliveNeighbours += GetStateOfCell(pos.Value.x + 1, pos.Value.z) ? 1 : 0;
            // SE
            aliveNeighbours += GetStateOfCell(pos.Value.x + 1, pos.Value.z - 1) ? 1 : 0;
            // S
            aliveNeighbours += GetStateOfCell(pos.Value.x, pos.Value.z - 1) ? 1 : 0;
            // SW
            aliveNeighbours += GetStateOfCell(pos.Value.x - 1, pos.Value.z - 1) ? 1 : 0;
            // W
            aliveNeighbours += GetStateOfCell(pos.Value.x - 1, pos.Value.z) ? 1 : 0;
            // NW
            aliveNeighbours += GetStateOfCell(pos.Value.x - 1, pos.Value.z + 1) ? 1 : 0;
            
            if (cell.IsAlive)
                cell.ChangeTo = aliveNeighbours == 2 || aliveNeighbours == 3;
            else
                cell.ChangeTo = aliveNeighbours == 3;
        }

        private bool GetStateOfCell(float x, float z)
        {
            if (x < 0)
                x = Width - 1;
            else if (x >= Width)
                x = 0;
            
            if (z < 0)
                z = Height - 1;
            else if (z >= Height)
                z = 0;
            
            var c = OtherCells[(int) (z + x * Height)];
            return c.IsAlive;
        }
    }

    private struct GetOtherCells : IJob
    {
        public NativeArray<CellComponent> Cells;
        public NativeArray<Translation> Positions;
        public int N;
        public int Height;
        public void Execute()
        {
            var cc = new NativeArray<CellComponent>(N, Allocator.Temp);
            var pc = new NativeArray<Translation>(N, Allocator.Temp);
            // Increment the size of the sub-arrays to be merged (sorted)
            for (int size = 1; size < N; size *= 2)
            {
                // Low Left index 
                int l1 = 0;
                // Index for inserting into temp arrays
                int k = 0;
                while (l1 + size < N)
                {
                    // High Left index
                    int h1 = l1 + size - 1;
                    // Left High Index
                    int l2 = h1 + 1;
                    // Right High Index
                    int h2 = l2 + size - 1;
                    // In case Right High extends further than the array length
                    if (h2 >= N)
                        h2 = N - 1;
                    
                    // Index for Left Array
                    int i = l1;
                    // Index for Right Array
                    int j = l2;
                    while (i <= h1 && j <= h2)
                    {
                        // Sort
                        if (CompCells(Positions[i], Positions[j], Height))
                        {
                            cc[k] = Cells[i];
                            pc[k] = Positions[i];
                            i++;
                            k++;
                        }
                        else
                        {
                            cc[k] = Cells[j];
                            pc[k] = Positions[j];
                            j++;
                            k++;
                        }
                    }
                    
                    // Insert the rest of the arrays
                    while (i <= h1)
                    {
                        cc[k] = Cells[i];
                        pc[k] = Positions[i];
                        i++;
                        k++;
                    }
                    while (j <= h2)
                    {
                        cc[k] = Cells[j];
                        pc[k] = Positions[j];
                        j++;
                        k++;
                    }
                    
                    // Increment the Left Low Array to the next one
                    l1 = h2 + 1;
                }

                for (int i = l1; k < N; i++)
                {
                    cc[k] = Cells[i];
                    pc[k] = Positions[i];
                    k++;
                }
                
                NativeArray<CellComponent>.Copy(cc, Cells);
                NativeArray<Translation>.Copy(pc, Positions);
            }
            cc.Dispose();
            pc.Dispose();
        }

        private bool CompCells(Translation a, Translation b, int height)
        {
            return a.Value.z + a.Value.x * height <= b.Value.z + b.Value.x * height;
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        // Sim started
        if (Input.GetKeyDown(KeyCode.Space))
            isRunning = !isRunning;
        if (!isRunning) 
            return inputDeps;

        // Time 
        timePassed += UnityEngine.Time.deltaTime;
        if (timePassed >= tickRate)
            timePassed = 0f;
        else
            return inputDeps;

        // Get other cells
        var otherCells = GetEntityQuery(ComponentType.ReadOnly<CellComponent>(), ComponentType.ReadOnly<Translation>());
        var cells = otherCells.ToComponentDataArray<CellComponent>(Allocator.Persistent);
        var pos = otherCells.ToComponentDataArray<Translation>(Allocator.Persistent);

        // Get other cells
        var getOtherCellsJob = new GetOtherCells()
        {
            Cells = cells,
            Positions = pos,
            N = cells.Length,
            Height = SpawnSystem.WorldSize.y
        }.Schedule(inputDeps);
        getOtherCellsJob.Complete();
        
        // Actual job
        var cellStateJob = new CellStateJob()
        {
            OtherCells = cells,
            Width = SpawnSystem.WorldSize.x,
            Height = SpawnSystem.WorldSize.y
        }.Schedule(this, getOtherCellsJob);
        
        cellStateJob.Complete();
        cells.Dispose();
        pos.Dispose();
        return cellStateJob;
    }
}
