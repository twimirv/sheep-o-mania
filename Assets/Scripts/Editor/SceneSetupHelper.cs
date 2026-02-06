using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;

public class SceneSetupHelper : Editor
{
    [MenuItem("Sheep-o-mania/Setup Scene")]
    public static void SetupScene()
    {
        // 0. Cleanup Old Objects
        GameObject oldGround = GameObject.Find("Ground");
        if (oldGround) DestroyImmediate(oldGround);
        
        GameObject oldAlpha = GameObject.Find("AlphaSheep");
        if (oldAlpha) DestroyImmediate(oldAlpha);
        
        GameObject oldFollowers = GameObject.Find("Followers");
        if (oldFollowers) DestroyImmediate(oldFollowers);
        
        GameObject oldCam = GameObject.FindGameObjectsWithTag("MainCamera").Length > 0 ? GameObject.FindGameObjectsWithTag("MainCamera")[0].gameObject : null;

        // 1. Create Ground Plane
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(5, 1, 5); // 50x50 area
        ground.transform.position = Vector3.zero;
        
        // Create and apply Green Checkerboard Material
        Texture2D checkerTexture = new Texture2D(256, 256);
        Color lightGreen = new Color(0.6f, 1.0f, 0.6f);
        Color darkGreen = new Color(0.3f, 0.8f, 0.3f);
        
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                // Create 8x8 checkerboard pattern
                bool isLight = ((x / 32) + (y / 32)) % 2 == 0;
                checkerTexture.SetPixel(x, y, isLight ? lightGreen : darkGreen);
            }
        }
        checkerTexture.filterMode = FilterMode.Point; // Keep edges sharp
        checkerTexture.Apply();

        Material groundMat = new Material(Shader.Find("Standard"));
        groundMat.mainTexture = checkerTexture;
        ground.GetComponent<Renderer>().material = groundMat;
        
        // 2. Spawn Alpha Sheep (Player)
        GameObject alphaSheep = new GameObject("AlphaSheep");
        CharacterController cc = alphaSheep.AddComponent<CharacterController>();
        PlayerInput pi = alphaSheep.AddComponent<PlayerInput>();
        
        // Load Input Actions
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (inputActions != null) {
            pi.actions = inputActions;
            pi.defaultActionMap = "Player";
        }

        AlphaSheepController alphaCtrl = alphaSheep.AddComponent<AlphaSheepController>();
        alphaSheep.AddComponent<SheepWiggle>(); // [NEW] Add Wiggle
        alphaSheep.transform.position = new Vector3(0, 0, 0);

        // Adjust Character Controller for a 3x Sheep
        cc.height = 2.4f;
        cc.center = new Vector3(0, 1.2f, 0);
        cc.radius = 0.9f;

        // Visuals for Alpha (Black Sheep)
        string sheepModelPath = "Assets/Nimble Fox/Generated Content/Models/Sheep_A/Sheep_A.fbx";
        GameObject sheepPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sheepModelPath);
        
        // Fallback search if path is wrong
        if (sheepPrefab == null) {
             string[] guids = AssetDatabase.FindAssets("Sheep_A t:Model");
             if (guids.Length > 0) sheepPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (sheepPrefab != null)
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(sheepPrefab);
            model.transform.SetParent(alphaSheep.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0, -90, 0); // Fix rotation
            model.transform.localScale = new Vector3(3, 3, 3); // 3x Scale

            // Apply Black Material
            Material blackMat = new Material(Shader.Find("Standard"));
            blackMat.color = Color.black;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.material = blackMat;
        }
        else
        {
            Debug.LogError("Could not find Sheep_A model!");
        }

        // 3. Spawn Followers
        int followerCount = 12; // At least 10
        Material whiteMat = new Material(Shader.Find("Standard"));
        whiteMat.color = Color.white;

        GameObject followersParent = new GameObject("Followers");

        for (int i = 0; i < followerCount; i++)
        {
            GameObject follower = new GameObject($"Follower_{i}");
            follower.transform.SetParent(followersParent.transform);
            
            // Random position on board
            float rx = Random.Range(-20f, 20f);
            float rz = Random.Range(-20f, 20f);
            follower.transform.position = new Vector3(rx, 0, rz);

            FollowerSheepController followCtrl = follower.AddComponent<FollowerSheepController>();
            follower.AddComponent<SheepWiggle>(); // [NEW] Add Wiggle
            CharacterController spawnCC = follower.AddComponent<CharacterController>();
            spawnCC.center = new Vector3(0, 1.2f, 0);
            spawnCC.height = 2.4f;
            spawnCC.radius = 0.9f;
            
            // Visuals
            if (sheepPrefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(sheepPrefab);
                model.transform.SetParent(follower.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0, 90, 0); // User specified 90
                model.transform.localScale = new Vector3(3, 3, 3); // 3x Scale
                
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers) r.material = whiteMat;
            }
            else
            {
                // Fallback
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.transform.SetParent(follower.transform);
                p.transform.localScale = new Vector3(1, 1, 2);
            }
        }

        // 4. Setup Camera
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }

        // Add CameraFollow 
        CameraFollow followScript = mainCam.gameObject.GetComponent<CameraFollow>();
        if (followScript == null)
            followScript = mainCam.gameObject.AddComponent<CameraFollow>();
        
        followScript.target = alphaSheep.transform; 
        
        // Align camera
        mainCam.transform.position = alphaSheep.transform.position + new Vector3(0, 10, -10);
        mainCam.transform.LookAt(alphaSheep.transform);

        Debug.Log($"Scene Setup Complete! Spawned Alpha Sheep and {followerCount} Followers.");
    }
}
