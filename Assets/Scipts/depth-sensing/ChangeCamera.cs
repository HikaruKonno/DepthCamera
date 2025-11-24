using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeCamera : MonoBehaviour
{
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Camera _subCamera;

    void Update()
    {
        // スペースを押したらカメラを切り替える
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_mainCamera.depth > _subCamera.depth)
            {
                _mainCamera.depth = 0;
                _subCamera.depth = 1;
            }
            else
            {
                _mainCamera.depth = 1;
                _subCamera.depth = 0;
            }
        }

        if(Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}
