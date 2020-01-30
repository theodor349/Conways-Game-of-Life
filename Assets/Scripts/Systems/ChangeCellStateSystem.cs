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
    public static float TickRate = 1f;
    
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
            // Make it tillable 
            if (x < 0)
                x = Width - 1;
            else if (x >= Width)
                x = 0;
            if (z < 0)
                z = Height - 1;
            else if (z >= Height)
                z = 0;
            
            // Return the state of that cell
            var c = OtherCells[(int) (z + x * Height)];
            return c.IsAlive;
        }
    }

    private struct SortOtherCellsJob : IJob
    {
        public NativeArray<CellComponent> Cells;
        public NativeArray<Translation> Positions;
        public int N;
        public int Height;
        public void Execute()
        {
            var cellCopy = new NativeArray<CellComponent>(N, Allocator.Temp);
            var posCopy = new NativeArray<Translation>(N, Allocator.Temp);
            // Increment the size of the sub-arrays to be merged (s=1, s=2, s=4...)
            for (int size = 1; size < N; size *= 2)
            {
                // Low Left index 
                int leftLowIndex = 0;
                // Index of temp arrays
                int k = 0;
                while (leftLowIndex + size < N)
                {
                    // Set Low and High Index for Left and Right side 
                    int leftHighIndex = leftLowIndex + size - 1;
                    int rightLowIndex = leftHighIndex + 1;
                    int rightHighIndex = rightLowIndex + size - 1;
                    // In case Right High extends further than the array length
                    if (rightHighIndex >= N)
                        rightHighIndex = N - 1;
                    
                    // Index for Left Array
                    int li = leftLowIndex;
                    // Index for Right Array
                    int ri = rightLowIndex;
                    while (li <= leftHighIndex && ri <= rightHighIndex)
                    {
                        // Sort
                        if (CompCells(Positions[li], Positions[ri], Height))
                        {
                            cellCopy[k] = Cells[li];
                            posCopy[k] = Positions[li];
                            li++;
                            k++;
                        }
                        else
                        {
                            cellCopy[k] = Cells[ri];
                            posCopy[k] = Positions[ri];
                            ri++;
                            k++;
                        }
                    }
                    
                    // Insert the rest of the arrays
                    while (li <= leftHighIndex)
                    {
                        cellCopy[k] = Cells[li];
                        posCopy[k] = Positions[li];
                        li++;
                        k++;
                    }
                    while (ri <= rightHighIndex)
                    {
                        cellCopy[k] = Cells[ri];
                        posCopy[k] = Positions[ri];
                        ri++;
                        k++;
                    }
                    
                    // Increment the Left Low Array to the next one
                    leftLowIndex = rightHighIndex + 1;
                }

                // Insert the rest of the array, in case not everything have been copied allready
                for (int i = leftLowIndex; k < N; i++)
                {
                    cellCopy[k] = Cells[i];
                    posCopy[k] = Positions[i];
                    k++;
                }
                
                NativeArray<CellComponent>.Copy(cellCopy, Cells);
                NativeArray<Translation>.Copy(posCopy, Positions);
            }
            cellCopy.Dispose();
            posCopy.Dispose();
        }

        private bool CompCells(Translation a, Translation b, int height)
        {
            // Sorts them in a Col by Col layout (0,0)(0,1)(1,0)(1,1)...
            return a.Value.z + a.Value.x * height <= b.Value.z + b.Value.x * height;
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        UpdateSimVariables();
        if (ShouldUpdate())
            return UpdateCells(inputDeps);
        return inputDeps;
    }

    private void UpdateSimVariables()
    {
        // Change speed
        if(Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals))
        {
            if (TickRate > 2)
                TickRate += 1;
            else 
                TickRate -= 0.1f;
            if (TickRate < 0.1f)
                TickRate = 0.1f;
        }
        else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            if (TickRate >= 2)
                TickRate += 1;
            else
                TickRate += 0.1f;
        }

        // Sim started
        if (Input.GetKeyDown(KeyCode.Space))
            isRunning = !isRunning;
    }
    
    private bool ShouldUpdate()
    {
        if (!isRunning) 
            return false;

        // Time 
        timePassed += UnityEngine.Time.deltaTime;
        if (timePassed >= TickRate)
            timePassed = 0f;
        else
            return false;
        
        return true;
    }

    private JobHandle UpdateCells(JobHandle inputDeps)
    {
        // Get other cells
        var otherCells = GetEntityQuery(ComponentType.ReadOnly<CellComponent>(), ComponentType.ReadOnly<Translation>());
        var cells = otherCells.ToComponentDataArray<CellComponent>(Allocator.Persistent);
        var pos = otherCells.ToComponentDataArray<Translation>(Allocator.Persistent);

        // Sort other cells
        var sortOtherCellsJob = new SortOtherCellsJob()
        {
            Cells = cells,
            Positions = pos,
            N = cells.Length,
            Height = SpawnSystem.WorldSize.y
        }.Schedule(inputDeps);
        sortOtherCellsJob.Complete();
        
        // Actual job
        var cellStateJob = new CellStateJob()
        {
            OtherCells = cells,
            Width = SpawnSystem.WorldSize.x,
            Height = SpawnSystem.WorldSize.y
        }.Schedule(this, sortOtherCellsJob);
        cellStateJob.Complete();
        
        // Remove unused memory
        pos.Dispose();
        cells.Dispose();
        return cellStateJob;
    }
}
