using UnityEngine;

//veri paketi
public class SousChefTask
{
    public SousChefCommand command;      
    public BaseCounter targetCounter;    
    public KitchenObjectSO targetItemSO; 
    public bool isCompleted;             

    // Constructor (Yapýcý Metot ) içine zorunlu olarak konacak bilgiler
    public SousChefTask(SousChefCommand cmd, BaseCounter counter, KitchenObjectSO itemSO = null)
    {
        command = cmd;
        targetCounter = counter;
        targetItemSO = itemSO;
        isCompleted = false; 
    }
}