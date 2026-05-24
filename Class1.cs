using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Mirror;
using TMPro;
using UnityEngine;

namespace MoreGamesBase
{
    public class FishingGame : GameBase
    {
        public Transform backgroundParent;
        public GameObject hookPrefab;
        public GameObject[] fishPrefabs;

        private readonly float fallSpeed = 0.5f;
        private readonly float minX = -0.4f;
        private readonly float maxX = 0.4f;
        private readonly float maxDepth = 8f;

        private GameObject hookInstance;
        private List<GameObject> activeFish = new List<GameObject>();
        private List<FishMovementData> activeFishMovement = new List<FishMovementData>();

        private float currentDepth = 0f;
        private float hookX = 0f;

        private Vector3 initialBackgroundPos = Vector3.zero;
        private bool hasCachedPosition = false;
        private float gameStartTime;

        private Coroutine activeResetCoroutine = null;

        private class FishMovementData
        {
            public GameObject fishInstance;
            public float speed;
            public int direction;
            public float leftBound;
            public float rightBound;
        }

        void Start()
        {
            CacheBackgroundPosition();

            if (hookPrefab != null && hookInstance == null)
            {
                hookInstance = Instantiate(hookPrefab, hookPrefab.transform);

                hookInstance.transform.localPosition = new Vector3(
                    hookInstance.transform.localPosition.x,
                    hookInstance.transform.localPosition.y,
                    hookInstance.transform.localPosition.z
                );

                if (isServer)
                {
                    Rigidbody2D rb = hookInstance.GetComponent<Rigidbody2D>();
                    if (rb == null) rb = hookInstance.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    HookCollisionRelay relay = hookInstance.GetComponent<HookCollisionRelay>();
                    if (relay == null) relay = hookInstance.AddComponent<HookCollisionRelay>();
                    relay.mainGameController = this;
                }
            }
        }

        private void CacheBackgroundPosition()
        {
            if (backgroundParent != null && !hasCachedPosition)
            {
                initialBackgroundPos = backgroundParent.localPosition;
                hasCachedPosition = true;
            }
        }

        public override bool Weaved() { return true; }

        // ==========================================
        // FRAMEWORK-COMPLIANT STRING RPCs
        // ==========================================
        public void BroadcastStartFall(string text)
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.FishingGame::UserRpcStartFall(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString(text);
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        public void BroadcastMoveHook(string text)
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.FishingGame::UserRpcMoveHook(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString(text);
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        public void BroadcastSpawnFish(string text)
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.FishingGame::UserRpcSpawnFish(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString(text);
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        public void BroadcastClearFish(string text)
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.FishingGame::UserRpcClearFish(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString(text);
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        public void BroadcastCatchFish(string text)
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.FishingGame::UserRpcCatchFish(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString(text);
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        // ==========================================
        // ENGINE GAME LOGIC
        // ==========================================
        protected override void StartGame()
        {
            if (activeResetCoroutine != null)
            {
                StopCoroutine(activeResetCoroutine);
                activeResetCoroutine = null;
                ForceVisualReset();
                this.ResetGame();
            }

            base.StartGame();
            CacheBackgroundPosition();

            if (base.isServer)
            {
                gameStartTime = Time.time;
                currentDepth = 0f;
                hookX = 0f;

                if (hookInstance != null) hookInstance.transform.localPosition = new Vector3(0, -1, 0);

                isPlaying = true;

                BroadcastClearFish("clear");
                BroadcastMoveHook("0");

                base.StartCoroutine(SpawnFishWithSpacing());

                BroadcastStartFall("start");
            }
        }

        public void UserRpcStartFall(string networkText)
        {
            if (!NetworkClient.active) return;
            CacheBackgroundPosition();
            currentDepth = 0f;
            hookX = 0f;
            if (hookInstance != null) hookInstance.transform.localPosition = new Vector3(0, -1, 0);
            isPlaying = true;
        }

        private void EndGame(bool won, float multiplier, string triggerReason = "Unknown")
        {
            if (!isPlaying) return;
            isPlaying = false;

            float timeAlive = Time.time - gameStartTime;

            if (base.isServer)
            {
                if (won && multiplier > 0f)
                {
                    this.Payout((double)multiplier, ChangeType.GameResult, null, -1L);
                }

                activeResetCoroutine = base.StartCoroutine(this.ResetGameRoutine());
            }
        }

        private void ForceVisualReset()
        {
            BroadcastClearFish("clear");

            currentDepth = 0f;
            hookX = 0f;

            if (backgroundParent != null && hasCachedPosition)
            {
                backgroundParent.localPosition = initialBackgroundPos;
            }

            if (hookInstance != null)
            {
                hookInstance.transform.localPosition = new Vector3(0f, -1f, 0f);
            }

            BroadcastMoveHook("0");
        }

        private IEnumerator ResetGameRoutine()
        {
            yield return new WaitForSeconds(2f);

            ForceVisualReset();
            activeResetCoroutine = null;

            this.ResetGame();
        }

        [Server]
        public void MoveLeft(PlayerInteract interact = null)
        {
            if (!NetworkServer.active || !isPlaying) return;
            hookX = Mathf.Clamp(hookX - 0.2f, minX, maxX);

            if (hookInstance != null)
            {
                hookInstance.transform.localPosition = new Vector3(hookX, hookInstance.transform.localPosition.y, hookInstance.transform.localPosition.z);
            }

            BroadcastMoveHook(hookX.ToString(CultureInfo.InvariantCulture));
        }

        [Server]
        public void MoveRight(PlayerInteract interact = null)
        {
            if (!NetworkServer.active || !isPlaying) return;
            hookX = Mathf.Clamp(hookX + 0.2f, minX, maxX);

            if (hookInstance != null)
            {
                hookInstance.transform.localPosition = new Vector3(hookX, hookInstance.transform.localPosition.y, hookInstance.transform.localPosition.z);
            }

            BroadcastMoveHook(hookX.ToString(CultureInfo.InvariantCulture));
        }

        public void UserRpcMoveHook(string networkText)
        {
            if (!NetworkClient.active || hookInstance == null) return;
            if (float.TryParse(networkText, NumberStyles.Any, CultureInfo.InvariantCulture, out float newX))
            {
                hookInstance.transform.localPosition = new Vector3(Mathf.Clamp(newX, minX, maxX), hookInstance.transform.localPosition.y, hookInstance.transform.localPosition.z);
            }
        }

        void Update()
        {
            if (!isPlaying) return;

            float safeDeltaTime = Time.deltaTime;

            currentDepth += fallSpeed * safeDeltaTime;

            if (backgroundParent != null)
            {
                backgroundParent.localPosition = new Vector3(
                    initialBackgroundPos.x,
                    initialBackgroundPos.y + currentDepth,
                    initialBackgroundPos.z
                );
            }

            for (int i = activeFishMovement.Count - 1; i >= 0; i--)
            {
                var fData = activeFishMovement[i];
                if (fData.fishInstance == null) continue;

                Vector3 localPos = fData.fishInstance.transform.localPosition;
                localPos.x += fData.speed * fData.direction * safeDeltaTime;
                fData.fishInstance.transform.localPosition = localPos;

                if (fData.direction == 1 && localPos.x >= fData.rightBound)
                {
                    fData.direction = -1;
                    FlipFishVisual(fData.fishInstance, false);
                }
                else if (fData.direction == -1 && localPos.x <= fData.leftBound)
                {
                    fData.direction = 1;
                    FlipFishVisual(fData.fishInstance, true);
                }
            }

            if (isServer && currentDepth >= maxDepth)
            {
                EndGame(false, 0f, "Depth Reached Max Limits");
            }
        }

        private void FlipFishVisual(GameObject fishObj, bool faceRight)
        {
            Vector3 scale = fishObj.transform.localScale;
            scale.x = faceRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            fishObj.transform.localScale = scale;

            TextMeshPro textMesh = fishObj.GetComponentInChildren<TextMeshPro>();
            if (textMesh != null)
            {
                Vector3 textScale = textMesh.transform.localScale;
                textScale.x = Mathf.Abs(textScale.x) * (scale.x < 0f ? -1f : 1f);
                textMesh.transform.localScale = textScale;
            }
        }

        public void HandleFishCollision(GameObject hitObject)
        {
            if (!isServer || !isPlaying) return;

            if (hitObject.GetComponent<TextMeshPro>() != null) return;

            float multiplier = 1.0f;
            string[] nameSplit = hitObject.name.Split('_');

            if (nameSplit.Length >= 3 && nameSplit[0] == "Fish")
            {
                float.TryParse(nameSplit[2], NumberStyles.Any, CultureInfo.InvariantCulture, out multiplier);
            }
            else
            {
                if (!hitObject.name.Contains("Fish") && hitObject.GetComponentInChildren<TextMeshPro>() == null) return;
            }

            BroadcastCatchFish(hitObject.name);

            EndGame(true, multiplier, $"Hook collided with {hitObject.name}");
        }

        private IEnumerator SpawnFishWithSpacing()
        {
            if (fishPrefabs == null || fishPrefabs.Length == 0) yield break;

            int fishDensityCount = 20;
            int maxFishIndex = Mathf.Min(19, fishPrefabs.Length - 1);

            for (int i = 0; i < fishDensityCount; i++)
            {
                float progress = (float)i / (fishDensityCount - 1);
                float assignedYHeight = Mathf.Lerp(-2.0f, -7.3f, progress);

                float exponent = Mathf.Lerp(4.0f, 0.2f, progress);
                float randomVal = Mathf.Pow(UnityEngine.Random.value, exponent);

                int fishTypeIndex = Mathf.RoundToInt(randomVal * maxFishIndex);
                fishTypeIndex = Mathf.Clamp(fishTypeIndex, 0, maxFishIndex);

                int startDir = UnityEngine.Random.Range(0, 2);
                float assignedSpeed = UnityEngine.Random.Range(0.3f, 0.7f);

                float multiplierReward = GetMultiplierForFishIndex(fishTypeIndex);

                string commandMessage = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:F2},{3:F2},{4:F2}",
                    fishTypeIndex, startDir, assignedSpeed, assignedYHeight, multiplierReward);

                BroadcastSpawnFish(commandMessage);
            }
            yield break;
        }

        private float GetMultiplierForFishIndex(int index)
        {
            int mult = 1;
            if (UnityEngine.Random.value < 0.003f) mult = 10;
            int fishNumber = index + 1;

            if (fishNumber >= 1 && fishNumber <= 5)
            {
                float t = (float)(fishNumber - 1) / 4f;
                return mult * Mathf.Lerp(0.1f, 0.25f, t);
            }
            else if (fishNumber >= 6 && fishNumber <= 11)
            {
                float t = (float)(fishNumber - 6) / 5f;
                return mult * Mathf.Lerp(0.25f, 0.5f, t);
            }
            else if (fishNumber == 12 || fishNumber == 13) return mult * UnityEngine.Random.Range(1.0f, 3.0f);
            else if (fishNumber == 14 || fishNumber == 15) return mult * 1.5f;
            else if (fishNumber == 16 || fishNumber == 17) return mult * 3.0f;
            else if (fishNumber == 18) return 0.0f;
            else if (fishNumber == 19) return 0.0f;
            else if (fishNumber == 20) return mult * 10.0f;

            return mult * 1.0f;
        }

        public void UserRpcSpawnFish(string networkText)
        {
            if (!NetworkClient.active || fishPrefabs == null || fishPrefabs.Length == 0) return;

            string[] data = networkText.Split(',');
            if (data.Length != 5) return;

            int index = int.Parse(data[0]);
            int startDir = int.Parse(data[1]);
            float speed = float.Parse(data[2], CultureInfo.InvariantCulture);
            float yPos = float.Parse(data[3], CultureInfo.InvariantCulture);
            float multiplier = float.Parse(data[4], CultureInfo.InvariantCulture);

            if (index < 0 || index >= fishPrefabs.Length) return;

            GameObject fishPrefab = fishPrefabs[index];
            if (fishPrefab == null) return;

            GameObject newFish = Instantiate(fishPrefab, backgroundParent);

            newFish.name = string.Format(CultureInfo.InvariantCulture, "Fish_{0}_{1:F2}_{2}", index, multiplier, Guid.NewGuid().ToString().Substring(0, 5));

            Vector3 bgScale = backgroundParent.localScale;
            newFish.transform.localScale = new Vector3(
                fishPrefab.transform.localScale.x / (bgScale.x != 0 ? bgScale.x : 1f),
                fishPrefab.transform.localScale.y / (bgScale.y != 0 ? bgScale.y : 1f),
                fishPrefab.transform.localScale.z / (bgScale.z != 0 ? bgScale.z : 1f)
            );

            float spawnLeftBoundaryX = -0.6f;
            float spawnRightBoundaryX = 0.6f;

            float initialX = (startDir == 0) ? spawnLeftBoundaryX : spawnRightBoundaryX;
            newFish.transform.localPosition = new Vector3(initialX, yPos, 0.1f);

            SpriteRenderer fishSR = newFish.GetComponent<SpriteRenderer>();
            if (fishSR == null) fishSR = newFish.GetComponentInChildren<SpriteRenderer>();
            if (fishSR != null) fishSR.sortingOrder = 10;

            int initialMovementDirectionSign = (startDir == 0) ? 1 : -1;
            FlipFishVisual(newFish, startDir == 0);

            TextMeshPro textMesh = newFish.GetComponentInChildren<TextMeshPro>();
            if (textMesh != null)
            {
                textMesh.text = multiplier.ToString("0.##", CultureInfo.InvariantCulture) + "x";
                textMesh.sortingOrder = 11;
                textMesh.transform.localPosition = new Vector3(textMesh.transform.localPosition.x, textMesh.transform.localPosition.y, -0.05f);

                textMesh.fontStyle = FontStyles.Bold;
                textMesh.outlineWidth = 0.4f;
                textMesh.outlineColor = Color.black;

                if (multiplier < 1.0f)
                {
                    textMesh.color = new Color(1.0f, 0.35f, 0.35f);
                }
                else if (multiplier >= 10.0f)
                {
                    textMesh.color = new Color(1.0f, 0.84f, 0.0f);
                }
                else
                {
                    textMesh.color = new Color(0.2f, 1.0f, 0.2f);
                }
            }

            activeFish.Add(newFish);

            activeFishMovement.Add(new FishMovementData
            {
                fishInstance = newFish,
                speed = speed,
                direction = initialMovementDirectionSign,
                leftBound = spawnLeftBoundaryX,
                rightBound = spawnRightBoundaryX
            });
        }

        public void UserRpcCatchFish(string networkText)
        {
            if (!NetworkClient.active) return;
            isPlaying = false; // Freeze map tracking instantly on client screen

            GameObject targetFish = activeFish.Find(f => f != null && f.name == networkText);
            if (targetFish != null)
            {
                StartCoroutine(AnimateCaughtFishSequence(targetFish));
            }
        }

        private IEnumerator AnimateCaughtFishSequence(GameObject fish)
        {
            if (fish == null) yield break;

            Vector3 startScale = fish.transform.localScale;
            Vector3 peakScale = startScale * 2.5f;

            Vector3 startLocalPos = fish.transform.localPosition;
            Vector3 targetLocalPos = new Vector3(0f, -currentDepth-1f, startLocalPos.z);

            float timeElapsed = 0f;
            float duration = 1.4f;

            while (timeElapsed < duration)
            {
                if (fish == null) yield break;
                timeElapsed += Time.deltaTime;
                float progressNormalized = timeElapsed / duration;

                float scaleCurve = Mathf.Sin(progressNormalized * Mathf.PI * 0.5f);
                fish.transform.localScale = Vector3.Lerp(startScale, peakScale, scaleCurve);

                float moveCurve = Mathf.SmoothStep(0f, 1f, progressNormalized);
                Vector3 currentMovingAnchor = Vector3.Lerp(startLocalPos, targetLocalPos, moveCurve);

                float currentShakeIntensity = Mathf.Lerp(0.08f, 0.0f, progressNormalized);
                Vector3 randomOffset = (Vector3)UnityEngine.Random.insideUnitCircle * currentShakeIntensity;

                fish.transform.localPosition = currentMovingAnchor + randomOffset;

                yield return null;
            }

            if (fish != null)
            {
                fish.transform.localPosition = targetLocalPos;
                fish.transform.localScale = peakScale;
            }
        }

        public void UserRpcClearFish(string networkText)
        {
            if (!NetworkClient.active) return;
            foreach (var fish in activeFish)
            {
                if (fish != null) Destroy(fish);
            }
            activeFish.Clear();
            activeFishMovement.Clear();
        }
    }

    public class HookCollisionRelay : MonoBehaviour
    {
        public FishingGame mainGameController;

        void OnTriggerEnter2D(Collider2D col)
        {
            if (mainGameController != null)
            {
                mainGameController.HandleFishCollision(col.gameObject);
            }
        }
    }
}