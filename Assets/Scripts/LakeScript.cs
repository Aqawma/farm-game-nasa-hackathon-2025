using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Diagnostics;
using Unity.Mathematics;

public class LakeScript : MonoBehaviour
{
    GameObject player;
    PlayerScript playerScript;

    [SerializeField] GameObject pipePrefab;
    [SerializeField] private int pipePrice;

    WaterReservoir waterReservoirScript;

    GameObject currentPipe = null;
    private bool interactable = false;

    float waterCapacity = 100000f;
    float waterFlowRate = 30;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        player = GameObject.FindWithTag("Player");
        playerScript = player.GetComponent<PlayerScript>();

        waterReservoirScript = GameObject.FindGameObjectWithTag("Reservoir").GetComponent<WaterReservoir>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            BuildPipe();
        }

        GiveWaterToReservoir();
    }

    void GiveWaterToReservoir()
    {
        float reservoirLevel = waterReservoirScript.currentWaterLevel;

        if (currentPipe != null && reservoirLevel < 1f)
        {
            float reservoirCapacity = waterReservoirScript.totalCapacity;

            float amountGiven = Math.Min(Math.Min(waterFlowRate, (0.99f - reservoirLevel) * reservoirCapacity), waterCapacity) * Time.deltaTime;
            waterCapacity -= amountGiven;
            waterReservoirScript.currentWaterLevel = (reservoirLevel * reservoirCapacity + amountGiven) / reservoirCapacity;
        }
    }

    void BuildPipe()
    {
        if (!interactable)
        {
            return;
        }

        playerScript.money -= pipePrice;

        currentPipe = Instantiate(pipePrefab, new Vector3(0, 0, 0), Quaternion.identity);
        currentPipe.transform.SetParent(transform);
        currentPipe.transform.localPosition = new Vector3(2, 0, 0);

    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.name == "Player")
        {
            interactable = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.name == "Player") 
        {
            interactable = false;
        }
    }
}
