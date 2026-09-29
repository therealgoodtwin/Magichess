using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Free-floating strategy-game camera, independent of the player. WASD pans
/// across the ground plane (screen-relative: W is always "up" on screen),
/// and Q/E snap-rotate 90 degrees around whatever point the camera is
/// currently looking at, so the view spins in place instead of drifting.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Tooltip("Units per second the camera pans at while a WASD key is held.")]
    [SerializeField] private float panSpeed = 10f;

    [Tooltip("Seconds to ramp up to/down from full pan speed - smooths out the start/stop snap of raw key presses.")]
    [SerializeField] private float panSmoothTime = 0.12f;

    [Tooltip("Degrees per second the camera turns at while orbiting.")]
    [SerializeField] private float orbitSpeed = 270f;

    [Tooltip("Height of the ground plane the camera pans across and orbits around.")]
    [SerializeField] private float groundHeight = 0f;

    [Header("Board Limits")]
    [Tooltip("Keeps the point the camera looks at inside the board, so it can't be panned off into empty space.")]
    [SerializeField] private bool limitToBoard = true;

    [Tooltip("How far past the outermost tile centres the view may go, in world units. Negative pulls the limit inwards.")]
    [SerializeField] private float boardMargin = 0.5f;

    private bool isOrbiting;
    private float remainingAngle;
    private float orbitSign;
    private Vector3 orbitPivot;

    private Vector3 currentPanVelocity;
    private Vector3 panVelocityRef;

    private bool isGliding;

    // Slides the camera so the given world point ends up on screen, then
    // calls onDone. Pan and orbit input are ignored until it lands. The point
    // lands lowerBy world units below screen centre (the camera looks that
    // far past it), staying inside the board when the board limit is on.
    public void GlideToFocus(Vector3 point, float duration, float lowerBy, System.Action onDone)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.0001f)
        {
            point += forward.normalized * lowerBy;
        }

        if (limitToBoard && RebuildBoardsIfNeeded())
        {
            Board board = NearestBoard(point);
            point.x = Mathf.Clamp(point.x, board.min.x - boardMargin, board.max.x + boardMargin);
            point.z = Mathf.Clamp(point.z, board.min.y - boardMargin, board.max.y + boardMargin);
        }

        if (isOrbiting)
        {
            RotateAround(orbitSign * remainingAngle);
            isOrbiting = false;
        }

        currentPanVelocity = Vector3.zero;
        panVelocityRef = Vector3.zero;

        StartCoroutine(Glide(point, duration, onDone));
    }

    private IEnumerator Glide(Vector3 point, float duration, System.Action onDone)
    {
        isGliding = true;

        Vector3 start = transform.position;
        Vector3 end = start + (point - FocusOnPlane(point.y));
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        transform.position = end;
        isGliding = false;
        onDone?.Invoke();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || isGliding)
        {
            return;
        }

        if (isOrbiting)
        {
            Orbit();
            return;
        }

        HandlePan(keyboard);

        if (keyboard.qKey.wasPressedThisFrame)
        {
            BeginOrbit(-90f);
        }
        else if (keyboard.eKey.wasPressedThisFrame)
        {
            BeginOrbit(90f);
        }
    }

    private void HandlePan(Keyboard keyboard)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 move = Vector3.zero;

        if (keyboard.wKey.isPressed)
        {
            move += forward;
        }

        if (keyboard.sKey.isPressed)
        {
            move -= forward;
        }

        if (keyboard.dKey.isPressed)
        {
            move += right;
        }

        if (keyboard.aKey.isPressed)
        {
            move -= right;
        }

        Vector3 targetVelocity = move.sqrMagnitude > 0f ? move.normalized * panSpeed : Vector3.zero;
        currentPanVelocity = Vector3.SmoothDamp(currentPanVelocity, targetVelocity, ref panVelocityRef, panSmoothTime);
        transform.position += currentPanVelocity * Time.deltaTime;

        if (limitToBoard)
        {
            ClampToBoard();
        }
    }

    // Limits the ground point the camera is looking at rather than the
    // camera itself: the camera sits well back from what it frames, so
    // clamping its own position would stop it at the wrong place - and Q/E
    // orbit around that same point, so the limit holds through rotation.
    // With several boards in the scene the view is held to whichever one it
    // is nearest to, so it can't wander across the gap between them.
    private void ClampToBoard()
    {
        if (!RebuildBoardsIfNeeded())
        {
            return;
        }

        Vector3 focus = GetGroundFocusPoint();
        Board board = NearestBoard(focus);

        float clampedX = Mathf.Clamp(focus.x, board.min.x - boardMargin, board.max.x + boardMargin);
        float clampedZ = Mathf.Clamp(focus.z, board.min.y - boardMargin, board.max.y + boardMargin);

        transform.position += new Vector3(clampedX - focus.x, 0f, clampedZ - focus.z);
    }

    private struct Board
    {
        public Vector2 min;
        public Vector2 max;
        public float height;
    }

    private readonly List<Board> boards = new();
    private int boardsBuiltForTileCount = -1;

    // Tiles that sit next to each other belong to the same board; a gap wider
    // than a tile or so starts a new one. Only redone when tiles come or go.
    private bool RebuildBoardsIfNeeded()
    {
        int count = Tile.All.Count;

        if (count == boardsBuiltForTileCount)
        {
            return boards.Count > 0;
        }

        boardsBuiltForTileCount = count;
        boards.Clear();

        if (count == 0)
        {
            return false;
        }

        Vector3[] positions = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            positions[i] = Tile.All[i].BaseWorldPosition;
        }

        float spacing = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                float distance = HorizontalDistance(positions[i], positions[j]);

                if (distance > 0.01f && distance < spacing)
                {
                    spacing = distance;
                }
            }
        }

        // 1.6 reaches diagonal neighbours (1.41 x spacing) but not the next tile over.
        float linkDistance = spacing * 1.6f;
        int[] group = new int[count];

        for (int i = 0; i < count; i++)
        {
            group[i] = i;
        }

        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (HorizontalDistance(positions[i], positions[j]) <= linkDistance)
                {
                    int a = Root(group, i);
                    int b = Root(group, j);

                    if (a != b)
                    {
                        group[b] = a;
                    }
                }
            }
        }

        Dictionary<int, int> boardIndexByRoot = new();
        List<int> tileCounts = new();

        for (int i = 0; i < count; i++)
        {
            int root = Root(group, i);

            if (!boardIndexByRoot.TryGetValue(root, out int index))
            {
                index = boards.Count;
                boardIndexByRoot[root] = index;
                boards.Add(new Board
                {
                    min = new Vector2(float.MaxValue, float.MaxValue),
                    max = new Vector2(float.MinValue, float.MinValue)
                });
                tileCounts.Add(0);
            }

            Board board = boards[index];
            board.min = Vector2.Min(board.min, new Vector2(positions[i].x, positions[i].z));
            board.max = Vector2.Max(board.max, new Vector2(positions[i].x, positions[i].z));
            board.height += positions[i].y;
            boards[index] = board;
            tileCounts[index]++;
        }

        for (int i = 0; i < boards.Count; i++)
        {
            Board board = boards[i];
            board.height /= tileCounts[i];
            boards[i] = board;
        }

        return true;
    }

    private static int Root(int[] group, int index)
    {
        while (group[index] != index)
        {
            group[index] = group[group[index]];
            index = group[index];
        }

        return index;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private Board NearestBoard(Vector3 point)
    {
        Board best = boards[0];
        float bestDistance = float.MaxValue;

        foreach (Board board in boards)
        {
            float dx = Mathf.Max(board.min.x - point.x, 0f, point.x - board.max.x);
            float dz = Mathf.Max(board.min.y - point.z, 0f, point.z - board.max.y);
            float distance = dx * dx + dz * dz;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = board;
            }
        }

        return best;
    }

    private void BeginOrbit(float angleDegrees)
    {
        orbitPivot = GetGroundFocusPoint();
        remainingAngle = Mathf.Abs(angleDegrees);
        orbitSign = Mathf.Sign(angleDegrees);
        isOrbiting = true;
    }

    private void Orbit()
    {
        float step = Mathf.Min(orbitSpeed * Time.deltaTime, remainingAngle);
        RotateAround(orbitSign * step);
        remainingAngle -= step;

        if (remainingAngle <= 0f)
        {
            isOrbiting = false;
        }
    }

    private void RotateAround(float angleDegrees)
    {
        Quaternion rotation = Quaternion.AngleAxis(angleDegrees, Vector3.up);
        transform.position = orbitPivot + rotation * (transform.position - orbitPivot);
        transform.rotation = rotation * transform.rotation;
    }

    private Vector3 GetGroundFocusPoint()
    {
        Vector3 focus = FocusOnPlane(groundHeight);

        // With the board limit on, "the ground" is the surface of the board
        // being looked at. A fixed height that's off from it would shift the
        // looked-at point along the view direction, so the limit would hold
        // too early on one side of a board and let the view slip out past
        // the opposite one.
        if (limitToBoard && RebuildBoardsIfNeeded())
        {
            focus = FocusOnPlane(NearestBoard(focus).height);
        }

        return focus;
    }

    private Vector3 FocusOnPlane(float height)
    {
        Ray ray = new Ray(transform.position, transform.forward);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, height, 0f));

        if (ground.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return transform.position + transform.forward * 10f;
    }
}
