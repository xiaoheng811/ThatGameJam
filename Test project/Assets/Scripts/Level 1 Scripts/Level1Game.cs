using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ThatGameJam.LevelOne
{
    public sealed class Level1Game : MonoBehaviour
    {
        [Serializable] public sealed class Note
        {
            public Transform model;
            public Vector2Int cell;
            public AudioClip clip;
            [NonSerialized] public bool collected;
            [NonSerialized] public AudioSource source;
            [NonSerialized] public Vector3 originalScale;
            [NonSerialized] public Vector3 offset;
            [NonSerialized] public Material[][] originals;
            [NonSerialized] public Renderer[] renderers;
            [NonSerialized] public Material[][] fading;
        }
        [Serializable] public sealed class Patrol
        {
            public Transform model;
            public Vector2Int home, other;
            public bool failOnReturnHit;
            [NonSerialized] public bool toOther;
            [NonSerialized] public float progress;
            [NonSerialized] public float wait;
            [NonSerialized] public Vector3 offset;
            [NonSerialized] public bool touching;
            [NonSerialized] public JellyObstacle jelly;
        }

        public Level1Grid grid;
        public Level1UI ui;
        public Transform player;
        public Transform goal;
        public Shader fadeShader;
        public Vector2Int startCell = new Vector2Int(4, 4);
        public Vector2Int goalCell = new Vector2Int(5, 4);
        public Note[] notes = {
            new Note { cell = new Vector2Int(4,6) },
            new Note { cell = new Vector2Int(2,2) },
            new Note { cell = new Vector2Int(7,4) }
        };
        public Patrol[] obstacles = {
            new Patrol { home = new Vector2Int(3,7), other = new Vector2Int(3,6) },
            new Patrol { home = new Vector2Int(6,7), other = new Vector2Int(5,7) },
            new Patrol { home = new Vector2Int(7,6), other = new Vector2Int(6,6), failOnReturnHit = true }
        };
        [Min(.1f)] public float jumpSeconds = .35f;
        [Min(0)] public float jumpHeight = .6f;
        [Min(.1f)] public float obstacleTravelSeconds = 1.3f;
        [Min(0)] public float obstacleWaitSeconds = .5f;
        [Min(.01f)] public float pushSeconds = .16f;
        [Min(.01f)] public float fadeSeconds = .45f;
        [Tooltip("Independent looping layers; disable for one-shot playback.")]
        public bool loopAudio = true;

        public bool Playing { get; private set; }
        public bool Paused => ui && ui.BlocksGameplay;
        public int Count { get; private set; }
        Vector2Int playerCell, target;
        Vector3 playerOffset, moveStart, moveEnd;
        Quaternion startRotation;
        Vector3 goalOffset;
        Renderer[] goalRenderers;
        Material[][] goalOriginals;
        Material yellowGoal;
        bool ready, moving, jumping;
        float moveTime, moveDuration;

        void Start()
        {
            if (!grid || !ui || !player || !goal || !fadeShader) { Bad("Missing Grid/UI/Player/Goal/Fade Shader reference."); return; }
            if (!grid.Build()) return;
            if (notes.Length != 3 || obstacles.Length != 3) { Bad("Exactly 3 notes and 3 obstacles required."); return; }
            if (!grid.Has(startCell) || !grid.Has(goalCell)) { Bad("Start or goal tile is missing."); return; }
            foreach (var n in notes)
                if (n == null || !n.model || !n.clip || !grid.Has(n.cell)) { Bad("Missing note object/audio/tile."); return; }
            foreach (var o in obstacles)
                if (o == null || !o.model || !grid.Has(o.home) || !grid.Has(o.other) ||
                    Mathf.Abs(o.home.x-o.other.x)+Mathf.Abs(o.home.y-o.other.y) != 1)
                { Bad("Obstacle needs a model and two adjacent existing tiles."); return; }
            if (!ui.ValidateReferences()) return;
            playerOffset = Vector3.up * (player.position.y - grid.Position(startCell).y);
            startRotation = player.rotation;
            goalOffset = Vector3.up * (goal.position.y - grid.Position(goalCell).y);
            PrepareBody(player);
            foreach (var n in notes)
            {
                PrepareBody(n.model);
                n.offset = Vector3.up * (n.model.position.y - grid.Position(n.cell).y);
                n.originalScale = n.model.localScale;
                n.renderers = n.model.GetComponentsInChildren<Renderer>(true);
                n.originals = new Material[n.renderers.Length][];
                n.fading = new Material[n.renderers.Length][];
                for (int r=0; r<n.renderers.Length; r++)
                {
                    n.originals[r] = n.renderers[r].sharedMaterials;
                    n.fading[r] = new Material[n.originals[r].Length];
                    for (int m=0; m<n.originals[r].Length; m++)
                    {
                        Material original = n.originals[r][m];
                        var fade = new Material(fadeShader);
                        if (original)
                        {
                            if (original.HasProperty("_BaseMap")) fade.SetTexture("_BaseMap", original.GetTexture("_BaseMap"));
                            else if (original.HasProperty("_MainTex")) fade.SetTexture("_BaseMap", original.GetTexture("_MainTex"));
                            if (original.HasProperty("_BaseColor")) fade.SetColor("_BaseColor", original.GetColor("_BaseColor"));
                            else if (original.HasProperty("_Color")) fade.SetColor("_BaseColor", original.color);
                        }
                        n.fading[r][m] = fade;
                    }
                }
                // Owned by the controller, so hiding a note never stops its audio.
                var audioObject = new GameObject("Level1Audio" + (Array.IndexOf(notes,n)+1));
                audioObject.transform.SetParent(transform, false);
                n.source = audioObject.AddComponent<AudioSource>();
                n.source.playOnAwake = false; n.source.loop = loopAudio;
                n.source.spatialBlend = 0; n.source.clip = n.clip;
            }
            foreach (var o in obstacles)
            {
                o.offset = Vector3.up * (o.model.position.y - grid.Position(o.home).y);
                PrepareBody(o.model);
                o.jelly = o.model.GetComponent<JellyObstacle>();
                if (o.jelly) o.jelly.Bind(this);
            }
            goalRenderers = goal.GetComponentsInChildren<Renderer>(true);
            goalOriginals = new Material[goalRenderers.Length][];
            for(int r=0; r<goalRenderers.Length; r++)
                goalOriginals[r] = goalRenderers[r].sharedMaterials;
            yellowGoal = new Material(fadeShader);
            yellowGoal.SetColor("_BaseColor",Color.yellow);
            ready = true;
            ui.Bind(this);
            ResetLevel(false);
        }
        void Bad(string message) { Debug.LogError("[Level1] " + message, this); enabled = false; }
        static void PrepareBody(Transform root)
        {
            foreach(var rb in root.GetComponentsInChildren<Rigidbody>(true))
            { rb.isKinematic = true; rb.useGravity = false; rb.detectCollisions = false; }
        }
        public void Play() { if(ready) ResetLevel(true); }
        public void Restart() { if(ready) ResetLevel(true); }
        public void MainMenu() { if(ready) ResetLevel(false); }

        void ResetLevel(bool play)
        {
            StopAllCoroutines();
            Playing = play; Count = 0; moving = false; jumping = false;
            playerCell = startCell; player.position = grid.Position(startCell) + playerOffset;
            player.rotation = startRotation;
            goal.position = grid.Position(goalCell) + goalOffset;
            goal.gameObject.SetActive(false);
            for(int r=0; r<goalRenderers.Length; r++)
                goalRenderers[r].sharedMaterials = goalOriginals[r];
            foreach(var n in notes)
            {
                n.collected=false; n.model.gameObject.SetActive(true);
                n.model.position=grid.Position(n.cell)+n.offset;
                n.model.localScale=n.originalScale;
                for(int r=0; r<n.renderers.Length; r++) n.renderers[r].sharedMaterials=n.originals[r];
                foreach(var list in n.fading) foreach(var mat in list) mat.SetFloat("_Fade",1);
                n.source.Stop(); n.source.volume=1f;
            }
            foreach(var o in obstacles)
            {
                o.toOther=true; o.progress=0; o.wait=obstacleWaitSeconds; o.touching=false;
                o.model.position=grid.Position(o.home)+o.offset;
                if (o.jelly) o.jelly.ResetWobble();
            }
            if (play) grid.HighlightPlayerTile(playerCell);
            else grid.ClearPlayerTileHighlight();
            ui.ResetView(play);
            RefreshUI();
        }
        void Update()
        {
            if(!ready || !Playing) return;
            bool paused=Paused;
            foreach(var n in notes)
            {
                if(!n.collected) continue;
                if(ui.AudioPaused) n.source.Pause(); else n.source.UnPause();
            }
            if(paused) return;
            float dt=Time.deltaTime;
            if(moving) AdvancePlayer(dt);
            // Swept obstacle movement catches a wall crossing the player's position.
            foreach(var o in obstacles)
            {
                if(!Playing) return;
                TickObstacle(o,dt);
            }
            if(!Playing || moving) return;
            Vector2Int dir=ReadDirection();
            if(dir != Vector2Int.zero)
            {
                Vector2Int destination=Level1Grid.JumpTarget(playerCell,dir);
                // Only the landing tile is checked: intermediate holes may be jumped over.
                if(grid.Has(destination)) BeginMove(destination,true);
            }
        }
        static Vector2Int ReadDirection()
        {
#if ENABLE_INPUT_SYSTEM
            var k=Keyboard.current;
            if(k==null) return Vector2Int.zero;
            if(k.wKey.wasPressedThisFrame) return Vector2Int.up;
            if(k.sKey.wasPressedThisFrame) return Vector2Int.down;
            if(k.aKey.wasPressedThisFrame) return Vector2Int.left;
            if(k.dKey.wasPressedThisFrame) return Vector2Int.right;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if(Input.GetKeyDown(KeyCode.W)) return Vector2Int.up;
            if(Input.GetKeyDown(KeyCode.S)) return Vector2Int.down;
            if(Input.GetKeyDown(KeyCode.A)) return Vector2Int.left;
            if(Input.GetKeyDown(KeyCode.D)) return Vector2Int.right;
#endif
            return Vector2Int.zero;
        }
        void BeginMove(Vector2Int cell,bool jump)
        {
            grid.ClearPlayerTileHighlight();
            target=cell; moving=true; jumping=jump; moveTime=0;
            moveDuration=jump ? jumpSeconds : pushSeconds;
            moveStart=player.position; moveEnd=grid.Position(cell)+playerOffset;
        }
        void AdvancePlayer(float dt)
        {
            moveTime+=dt;
            float t=Mathf.Clamp01(moveTime/Mathf.Max(.01f,moveDuration));
            player.position=Vector3.Lerp(moveStart,moveEnd,t)+Vector3.up*(jumping ? Mathf.Sin(t*Mathf.PI)*jumpHeight : 0);
            if(t<1) return;
            player.position=moveEnd; playerCell=target; moving=false; jumping=false;
            grid.HighlightPlayerTile(playerCell);
            // Landing on an occupied tile is resolved before collecting/winning.
            foreach(var o in obstacles)
                if(Near(o.model.position,player.position)) { Hit(o); return; }
            ResolveCell();
        }
        bool Near(Vector3 a,Vector3 b)
        { a.y=0; b.y=0; return Vector3.Distance(a,b)<grid.CellSize*.38f; }
        void TickObstacle(Patrol o,float dt)
        {
            Vector3 before=o.model.position;
            if(o.wait>0) o.wait=Mathf.Max(0,o.wait-dt);
            else
            {
                o.progress=Mathf.Clamp01(o.progress+dt/Mathf.Max(.1f,obstacleTravelSeconds));
                var from=o.toOther ? o.home : o.other;
                var to=o.toOther ? o.other : o.home;
                o.model.position=Vector3.Lerp(grid.Position(from),grid.Position(to),o.progress)+o.offset;
            }
            bool hit=false;
            if(!moving)
            {
                Vector3 a=before, b=o.model.position, p=player.position;
                a.y=b.y=p.y=0;
                Vector3 ab=b-a;
                Vector3 closest=a+ab*(ab.sqrMagnitude<.00001f ? 0 : Mathf.Clamp01(Vector3.Dot(p-a,ab)/ab.sqrMagnitude));
                hit=Vector3.Distance(closest,p)<grid.CellSize*.38f;
            }
            if(hit && !o.touching) { o.touching=true; Hit(o); }
            else if(!Near(o.model.position,player.position)) o.touching=false;
            if(o.progress>=1) { o.progress=0; o.toOther=!o.toOther; o.wait=obstacleWaitSeconds; }
        }
        void Hit(Patrol o)
        {
            if(!Playing || moving) return;
            if (o.jelly) o.jelly.Impact(player.position - o.model.position);
            // Obstacle03 returning to (7,6) is explicitly fatal, even if (8,6) exists.
            if(o.failOnReturnHit && playerCell==o.home && (!o.toOther || o.wait>0)) { Fail(); return; }
            Vector2Int direction=o.toOther ? o.other-o.home : o.home-o.other;
            // At a waiting endpoint, push outward in its last arrival direction.
            if(o.wait>0) direction=-direction;
            Vector2Int pushed=playerCell+direction;
            if(!grid.Has(pushed)) { Fail(); return; }
            foreach(var other in obstacles)
                if(other!=o && Near(other.model.position,grid.Position(pushed))) { Fail(); return; }
            BeginMove(pushed,false);
        }
        void Fail()
        {
            Debug.Log("[Level1] Player pushed out or crushed. Restarting.",this);
            ResetLevel(true);
        }
        void ResolveCell()
        {
            foreach(var n in notes)
            {
                if(n.collected || n.cell!=playerCell) continue;
                n.collected=true; Count++;
                n.source.Play();
                StartCoroutine(Fade(n));
            }
            if(Count==3) goal.gameObject.SetActive(true);
            RefreshUI();
            if(Count==3 && playerCell==goalCell)
            {
                foreach(var r in goalRenderers)
                {
                    var yellow=new Material[r.sharedMaterials.Length];
                    for(int m=0;m<yellow.Length;m++) yellow[m]=yellowGoal;
                    r.sharedMaterials=yellow;
                }
                Playing=false;
                // No Time.timeScale changes and no AudioSource.Stop: music keeps playing.
                ui.ShowWin();
            }
        }
        IEnumerator Fade(Note n)
        {
            for(int r=0; r<n.renderers.Length; r++) n.renderers[r].sharedMaterials=n.fading[r];
            float t=0;
            while(t<fadeSeconds)
            {
                if(!Paused) t+=Time.deltaTime;
                float alpha=1-Mathf.Clamp01(t/Mathf.Max(.01f,fadeSeconds));
                foreach(var list in n.fading) foreach(var mat in list) mat.SetFloat("_Fade",alpha);
                yield return null;
            }
            n.model.gameObject.SetActive(false);
        }
        void RefreshUI() { ui.Refresh(Count,notes); }
        public void SetVolume(int index,float volume)
        { if(ready && index>=0 && index<notes.Length && notes[index].collected) notes[index].source.volume=Mathf.Clamp01(volume); }
        void OnDestroy()
        {
            if (grid) grid.ClearPlayerTileHighlight();
            foreach(var n in notes) if(n!=null && n.fading!=null)
                foreach(var list in n.fading) foreach(var mat in list) if(mat) Destroy(mat);
            if(yellowGoal) Destroy(yellowGoal);
        }
    }
}


