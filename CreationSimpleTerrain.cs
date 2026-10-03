using UnityEditor;

using UnityEngine;

using System.Collections;
using System.Collections.Generic;

using UnityEngine.InputSystem;

using static CreationSimpleTerrain;

using System.Security.Cryptography;



[RequireComponent(typeof(MeshFilter))]

[RequireComponent(typeof(MeshRenderer))]

[RequireComponent(typeof(MeshCollider))]

public class CreationSimpleTerrain : MonoBehaviour

{

    [Range(1, 2000)]

    public float dimension = 100;



    [Range(1, 15)]

    [Tooltip("La resolution sera 2 élevé à la puissance de cette valeur 2^n ")]

    public int puissance2Resolution = 1;



    public bool CentrerPivot = true;



    [HideInInspector] public ushort resolution;



    [Tooltip("Information sur le mode de génération :")]

    public enum ChoixModeDeformation

    {

        Fonction,

        Texture

    }



    public ChoixModeDeformation choixModeDeformation;



    public enum TypeFonction

    {

        Sinusoide,

        Collines,

        Perlin

    }



    public enum TypeNormale

    {

        Moyenne,

        Surface,

        Angle

    }



    public enum TypeAffichageNormale

    {

        Vertices,

        Eclairage,

        Orientation,

        EclairageOrientation

    }



    public TypeNormale typeNormale;



    [Tooltip("Choix de la première fonction à appliquer")]

    public TypeFonction typeFonction;



    [Tooltip("Choix de la première HeightMap à appliquer")]

    public int numTexture;



    [Tooltip("Les HeightMaps disponibles")]

    public List<Texture2D> textures;



    public Texture2D heightMap;



    private TypeAffichageNormale typeAffichageNormale = TypeAffichageNormale.EclairageOrientation;

    private float tempsAffichageNormales = 0;



    private uint p_dimVertices;

    private uint p_dimTriangles;



    private MeshCollider p_meshCollider;

    private MeshFilter p_meshFilter;

    private Mesh p_mesh;

    private Mesh p_meshLOD1;

    private Mesh p_meshLOD2;

    private Vector3[] p_vertices;

    private Vector3[] p_normals;

    private int[] p_triangles;

    private Vector2[] p_uv;

    private List<List<int>> p_trianglesParVertex;



    private float fps;

    private bool afficherAide = false;



    private Camera p_cam;

    private LayerMask maskPickingTerrain;

    private Vector3 positionClic;

    private float p_dimInterVertices;

    // =========================
    // EXERCICE 5 : CHUNKS
    // =========================

    [Header("Exercice 5 - Chunks")]
    public bool estChunkPrincipal = true;
    public float rayonDeformationChunk = 25f;
    public float dureeSurlignageChunks = 3f;

    private Dictionary<Vector2Int, CreationSimpleTerrain> chunks = new Dictionary<Vector2Int, CreationSimpleTerrain>();
    private Dictionary<CreationSimpleTerrain, Material> materiauxOriginaux = new Dictionary<CreationSimpleTerrain, Material>();
    private float tempsSurlignageChunks = 0f;

    // Historique des déformations effectuées en espace monde.
    // Il permet à un chunk créé après une déformation de récupérer
    // la partie de cette déformation qui concerne sa surface.
    private struct DeformationEnregistree
    {
        public Vector3 position;
        public float rayon;

        public DeformationEnregistree(Vector3 position, float rayon)
        {
            this.position = position;
            this.rayon = rayon;
        }
    }

    private List<DeformationEnregistree> historiqueDeformations = new List<DeformationEnregistree>();




    void Reset()

    {

        if (GetComponent<MeshFilter>() == null)

            EditorUtility.DisplayDialog("Le component MeshFilter est nécessaire à la sélection", "MeshCollider ajouté au gameObject ! ", "j'ai compris ! ");



        gameObject.AddComponent<MeshFilter>();



        Collider[] colliders = GetComponents<Collider>();



        if (colliders.Length > 0)

        {

            foreach (Collider collider in colliders)

                DestroyImmediate(collider);



            EditorUtility.DisplayDialog("Un seul MeshCollider", "Tous les collider sont supprimés ; seuls un meshCollider est associé au terrain", "OK ! ");

        }

        else

            EditorUtility.DisplayDialog("Le component MeshCollider est nécessaire à la sélection", "MeshCollider ajouté au gameObject ! ", "j'ai compris ! ");



        p_meshCollider = gameObject.AddComponent<MeshCollider>();



        if (GetComponent<MeshRenderer>() == null)

        {

            EditorUtility.DisplayDialog("Le component MeshRenderer est nécessaire à la sélection", "MeshCollider ajouté au gameObject ! ", "j'ai compris ! ");

            gameObject.AddComponent<MeshRenderer>();

        }

    }



    void Awake()

    {

        p_cam = Camera.main;



        // les rayCast exploiteront cette layer pour ne tester la collision qu'avec l'objet terrain

        gameObject.layer = LayerMask.NameToLayer("L_PickingTerrain");

        maskPickingTerrain = LayerMask.GetMask("L_PickingTerrain");



        /* A COMPLETER !! */

    }



    private void deplacerCamera()

    {

        float vitesse = 10f;



        if (Keyboard.current.wKey.isPressed)

        {

            p_cam.transform.position += p_cam.transform.forward * vitesse * Time.deltaTime;

        }



        if (Keyboard.current.sKey.isPressed)

        {

            p_cam.transform.position -= p_cam.transform.forward * vitesse * Time.deltaTime;

        }



        if (Keyboard.current.aKey.isPressed)

        {

            p_cam.transform.position -= p_cam.transform.right * vitesse * Time.deltaTime;

        }



        if (Keyboard.current.dKey.isPressed)

        {

            p_cam.transform.position += p_cam.transform.right * vitesse * Time.deltaTime;

        }

    }



    private void tournerTerrain()

    {

        if (Mouse.current.rightButton.isPressed)

        {

            float mouvementSouris = Mouse.current.delta.ReadValue().x;

            transform.Rotate(Vector3.up, mouvementSouris);

        }

    }



    private void appliquerDeformation_Fonction()

    {

        switch (typeFonction)

        {

            case TypeFonction.Sinusoide:

                {

                    float amplitude = Random.Range(1f, 20f);

                    float frequence = Random.Range(0.1f, 1f);



                    for (int i = 0; i < p_vertices.Length; i++)

                    {

                        float hauteur = amplitude * Mathf.Sin(frequence * p_vertices[i].x);

                        p_vertices[i].y = hauteur;

                    }



                    p_mesh.SetVertices(p_vertices);

                    break;

                }



            case TypeFonction.Collines:

                {

                    float centreX = positionClic.x;

                    float centreZ = positionClic.z;

                    float hauteurMin = 0;

                    float hauteurMax = 20;

                    float sigma = 25;



                    for (int i = 0; i < p_vertices.Length; i++)

                    {

                        float ecartHorizontal;

                        float ecartVertical;



                        ecartHorizontal = p_vertices[i].x - centreX;

                        ecartVertical = p_vertices[i].z - centreZ;



                        float distance = Mathf.Sqrt(

                            ecartHorizontal * ecartHorizontal +

                            ecartVertical * ecartVertical

                        );



                        float distanceCarre = Mathf.Pow(distance, 2);

                        float sigmaCarre = Mathf.Pow(sigma, 2);

                        float fraction = distanceCarre / (2 * sigmaCarre);

                        float valeurGaussienne = Mathf.Exp(-fraction);



                        float hauteur = hauteurMin +

                            (hauteurMax - hauteurMin) * valeurGaussienne;



                        p_vertices[i].y = hauteur;

                    }



                    p_mesh.SetVertices(p_vertices);

                    break;

                }



            case TypeFonction.Perlin:

                {

                    float frequence = Random.Range(0.1f, 0.75f);

                    float amplitude = Random.Range(1f, 20f);



                    for (int i = 0; i < p_vertices.Length; i++)

                    {

                        float hauteur = Mathf.PerlinNoise(

                            frequence * p_vertices[i].x,

                            frequence * p_vertices[i].z

                        ) * amplitude;



                        p_vertices[i].y = hauteur;

                    }



                    p_mesh.SetVertices(p_vertices);

                    break;

                }

        }

    }



    private void appliquerDeformation_Texture()

    {

        float amplitude = 20;



        int largeurTexture = textures[numTexture].width;

        int hauteurTexture = textures[numTexture].height;



        for (int i = 0; i < p_vertices.Length; i++)

        {

            int xPixel = (int)(p_uv[i].x * (largeurTexture - 1));

            int yPixel = (int)(p_uv[i].y * (hauteurTexture - 1));



            Color couleur = textures[numTexture].GetPixel(xPixel, yPixel);



            float niveauGris = (couleur.r + couleur.g + couleur.b) / 3;

            float hauteur = niveauGris * amplitude;



            p_vertices[i].y = hauteur;

        }



        for (uint i = 0; i < p_vertices.Length; i++)

        {

            calculerNormaleVertex(i);

        }



        p_mesh.SetVertices(p_vertices);

        p_mesh.SetNormals(p_normals);

    }



    private void calculerNormaleVertex(uint num_Vertex)

    {

        switch (typeNormale)

        {

            case TypeNormale.Moyenne:

                {

                    Vector3 sommeNormales = Vector3.zero;

                    int nombreTriangles = 0;



                    foreach (int triangleIndex in p_trianglesParVertex[(int)num_Vertex])

                    {

                        int i = triangleIndex * 3;



                        Vector3 A = p_vertices[p_triangles[i]];

                        Vector3 B = p_vertices[p_triangles[i + 1]];

                        Vector3 C = p_vertices[p_triangles[i + 2]];



                        Vector3 v1 = B - A;

                        Vector3 v2 = C - A;



                        Vector3 normale = Vector3.Cross(v1, v2).normalized;



                        sommeNormales += normale;

                        nombreTriangles++;

                    }



                    if (nombreTriangles > 0)

                    {

                        Vector3 normaleMoyenne = sommeNormales / nombreTriangles;

                        normaleMoyenne = normaleMoyenne.normalized;

                        p_normals[(int)num_Vertex] = normaleMoyenne;

                    }

                    else

                    {

                        p_normals[(int)num_Vertex] = Vector3.up;

                    }



                    break;

                }



            case TypeNormale.Surface:

                {

                    Vector3 sommeNormales = Vector3.zero;

                    float sommeSurfaces = 0;



                    foreach (int triangleIndex in p_trianglesParVertex[(int)num_Vertex])

                    {

                        int i = triangleIndex * 3;



                        Vector3 A = p_vertices[p_triangles[i]];

                        Vector3 B = p_vertices[p_triangles[i + 1]];

                        Vector3 C = p_vertices[p_triangles[i + 2]];



                        Vector3 v1 = B - A;

                        Vector3 v2 = C - A;



                        Vector3 cross = Vector3.Cross(v1, v2);

                        float surface = cross.magnitude / 2;

                        Vector3 normale = cross.normalized;



                        sommeNormales += normale * surface;

                        sommeSurfaces += surface;

                    }



                    if (sommeSurfaces > 0)

                    {

                        Vector3 normaleMoyenne = sommeNormales / sommeSurfaces;

                        normaleMoyenne = normaleMoyenne.normalized;

                        p_normals[(int)num_Vertex] = normaleMoyenne;

                    }

                    else

                    {

                        p_normals[(int)num_Vertex] = Vector3.up;

                    }



                    break;

                }



            case TypeNormale.Angle:

                {

                    Vector3 sommeNormales = Vector3.zero;

                    float sommeAngles = 0;



                    foreach (int triangleIndex in p_trianglesParVertex[(int)num_Vertex])

                    {

                        int i = triangleIndex * 3;



                        Vector3 A = p_vertices[p_triangles[i]];

                        Vector3 B = p_vertices[p_triangles[i + 1]];

                        Vector3 C = p_vertices[p_triangles[i + 2]];



                        Vector3 v1;

                        Vector3 v2;



                        if (num_Vertex == p_triangles[i])

                        {

                            v1 = B - A;

                            v2 = C - A;

                        }

                        else if (num_Vertex == p_triangles[i + 1])

                        {

                            v1 = A - B;

                            v2 = C - B;

                        }

                        else

                        {

                            v1 = A - C;

                            v2 = B - C;

                        }



                        Vector3 cross = Vector3.Cross(B - A, C - A);

                        Vector3 normale = cross.normalized;



                        float angle = Vector3.Angle(v1, v2);



                        sommeNormales += normale * angle;

                        sommeAngles += angle;

                    }



                    if (sommeAngles > 0)

                    {

                        Vector3 normaleMoyenne = sommeNormales / sommeAngles;

                        normaleMoyenne = normaleMoyenne.normalized;

                        p_normals[(int)num_Vertex] = normaleMoyenne;

                    }

                    else

                    {

                        p_normals[(int)num_Vertex] = Vector3.up;

                    }



                    break;

                }

        }

    }



    private void bakingTriangles()

    {

        p_trianglesParVertex = new List<List<int>>(p_vertices.Length);



        for (int i = 0; i < p_vertices.Length; i++)

        {

            p_trianglesParVertex.Add(new List<int>());

        }



        for (int i = 0; i < p_triangles.Length; i += 3)

        {

            int triangleIndex = i / 3;



            int vertexA = p_triangles[i];

            int vertexB = p_triangles[i + 1];

            int vertexC = p_triangles[i + 2];



            p_trianglesParVertex[vertexA].Add(triangleIndex);

            p_trianglesParVertex[vertexB].Add(triangleIndex);

            p_trianglesParVertex[vertexC].Add(triangleIndex);

        }

    }

    private Mesh creerMeshLOD(int resolutionLOD)

{

    int dimVertices = resolutionLOD * resolutionLOD;

    int dimTriangles = 2 * (resolutionLOD - 1) * (resolutionLOD - 1);



    Vector3[] vertices = new Vector3[dimVertices];

    Vector3[] normals = new Vector3[dimVertices];

    Vector2[] uv = new Vector2[dimVertices];



    float dimInterVertices = dimension / (resolutionLOD - 1);



    for (int z = 0; z < resolutionLOD; z++)

    {

        for (int x = 0; x < resolutionLOD; x++)

        {

            float xPosition = x * dimInterVertices;
            float zPosition = z * dimInterVertices;

            float u = (float)x / (resolutionLOD - 1);
            float v = (float)z / (resolutionLOD - 1);
            float yPosition = 0f;

            if (p_vertices != null && p_vertices.Length == resolution * resolution)
            {
                int terrainX = Mathf.RoundToInt(u * (resolution - 1));
                int terrainZ = Mathf.RoundToInt(v * (resolution - 1));
                int terrainIndex = terrainZ * resolution + terrainX;
                yPosition = p_vertices[terrainIndex].y;
            }



            int index = z * resolutionLOD + x;



            normals[index] = Vector3.up;

            vertices[index] = new Vector3(xPosition, yPosition, zPosition);

            uv[index] = new Vector2(u, v);

        }

    }



    int[] triangles = new int[dimTriangles * 3];



    int indexTriangle = 0;



    for (int z = 0; z < resolutionLOD - 1; z++)

    {

        for (int x = 0; x < resolutionLOD - 1; x++)

        {

            int i = z * resolutionLOD + x;



            triangles[indexTriangle++] = i;

            triangles[indexTriangle++] = i + resolutionLOD;

            triangles[indexTriangle++] = i + 1;



            triangles[indexTriangle++] = i + resolutionLOD + 1;

            triangles[indexTriangle++] = i + 1;

            triangles[indexTriangle++] = i + resolutionLOD;

        }

    }



    Mesh meshLOD = new Mesh();



    meshLOD.SetVertices(vertices);

    meshLOD.SetTriangles(triangles, 0);

    meshLOD.SetNormals(normals);

    meshLOD.SetUVs(0, uv);



    return meshLOD;

}

    private void creerLeMeshTerrain()

{

    resolution = (ushort)Mathf.Pow(2, puissance2Resolution);

    p_dimVertices = (uint)(resolution * resolution);

    p_dimTriangles = (uint)(2 * (resolution - 1) * (resolution - 1));

    p_dimInterVertices = dimension / (resolution - 1);



    p_vertices = new Vector3[(int)p_dimVertices];

    p_normals = new Vector3[(int)p_dimVertices];

    p_uv = new Vector2[(int)p_dimVertices];



    for (int z = 0; z < resolution; z++)

    {

        for (int x = 0; x < resolution; x++)

        {

            float xPosition = x * p_dimInterVertices;

            float zPosition = z * p_dimInterVertices;

            float yPosition = 0;



            float u = (float)x / (resolution - 1);

            float v = (float)z / (resolution - 1);



            int index = z * resolution + x;



            p_normals[index] = Vector3.up;

            p_vertices[index] = new Vector3(xPosition, yPosition, zPosition);

            p_uv[index] = new Vector2(u, v);

        }

    }



    p_triangles = new int[(int)p_dimTriangles * 3];



    int indexTriangle = 0;



    for (int z = 0; z < resolution - 1; z++)

    {

        for (int x = 0; x < resolution - 1; x++)

        {

            int i = z * resolution + x;



            p_triangles[indexTriangle++] = i;

            p_triangles[indexTriangle++] = i + resolution;

            p_triangles[indexTriangle++] = i + 1;



            p_triangles[indexTriangle++] = i + resolution + 1;

            p_triangles[indexTriangle++] = i + 1;

            p_triangles[indexTriangle++] = i + resolution;

        }

    }



    bakingTriangles();



    for (uint i = 0; i < p_vertices.Length; i++)

    {

        calculerNormaleVertex(i);

    }



    p_mesh = new Mesh();



    p_mesh.SetVertices(p_vertices);

    p_mesh.SetTriangles(p_triangles, 0);

    p_mesh.SetNormals(p_normals);

    p_mesh.SetUVs(0, p_uv);



    p_meshFilter = GetComponent<MeshFilter>();

    p_meshFilter.mesh = p_mesh;



    MeshCollider meshCollider = GetComponent<MeshCollider>();

    meshCollider.sharedMesh = p_mesh;



    p_meshLOD1 = creerMeshLOD(Mathf.Max(16, (int)Mathf.Pow(2, Mathf.Max(4, puissance2Resolution - 3))));

    p_meshLOD2 = creerMeshLOD(16);



    GameObject lod1 = new GameObject("LOD1");

    GameObject lod2 = new GameObject("LOD2");



    lod1.transform.SetParent(transform);

    lod2.transform.SetParent(transform);



    MeshFilter meshFilterLOD1 = lod1.AddComponent<MeshFilter>();

    MeshFilter meshFilterLOD2 = lod2.AddComponent<MeshFilter>();



    MeshRenderer meshRendererLOD1 = lod1.AddComponent<MeshRenderer>();

    MeshRenderer meshRendererLOD2 = lod2.AddComponent<MeshRenderer>();



    MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

    meshRendererLOD1.sharedMaterial = meshRenderer.sharedMaterial;
    meshRendererLOD2.sharedMaterial = meshRenderer.sharedMaterial;

    LODGroup lodGroup = gameObject.AddComponent<LODGroup>();

    LOD lod0 = new LOD(0.6f, new Renderer[] { meshRenderer });
    LOD lodNiveau1 = new LOD(0.3f, new Renderer[] { meshRendererLOD1 });
    LOD lodNiveau2 = new LOD(0.1f, new Renderer[] { meshRendererLOD2 });

    lodGroup.SetLODs(new LOD[] { lod0, lodNiveau1, lodNiveau2 });
    lodGroup.RecalculateBounds();

    meshFilterLOD1.mesh = p_meshLOD1;
    meshFilterLOD2.mesh = p_meshLOD2;
}

    // ================================================================
    // EXERCICE 5 - GESTION DES CHUNKS
    // ================================================================

    private void CreerChunk(Vector2Int coordonnee)
    {
        if (chunks.ContainsKey(coordonnee)) return;

        GameObject nouvelObjet = new GameObject("Chunk_" + coordonnee.x + "_" + coordonnee.y);
        CreationSimpleTerrain nouveauChunk = nouvelObjet.AddComponent<CreationSimpleTerrain>();

        nouveauChunk.estChunkPrincipal = false;
        nouveauChunk.dimension = dimension;
        nouveauChunk.puissance2Resolution = puissance2Resolution;
        nouveauChunk.CentrerPivot = CentrerPivot;
        nouveauChunk.choixModeDeformation = choixModeDeformation;
        nouveauChunk.typeNormale = typeNormale;
        nouveauChunk.typeFonction = typeFonction;
        nouveauChunk.numTexture = numTexture;
        nouveauChunk.textures = textures;
        nouveauChunk.heightMap = heightMap;
        nouveauChunk.rayonDeformationChunk = rayonDeformationChunk;
        nouveauChunk.dureeSurlignageChunks = dureeSurlignageChunks;

        // Tous les chunks partagent le même historique de déformations.
        // Le nouveau chunk pourra ainsi rejouer les anciennes déformations
        // une fois son mesh créé dans Start().
        nouveauChunk.historiqueDeformations = historiqueDeformations;

        nouvelObjet.transform.position = transform.position + new Vector3(coordonnee.x * dimension, 0f, coordonnee.y * dimension);

        MeshRenderer rendererPrincipal = GetComponent<MeshRenderer>();
        MeshRenderer rendererNouveau = nouvelObjet.GetComponent<MeshRenderer>();
        if (rendererPrincipal != null && rendererNouveau != null)
            rendererNouveau.sharedMaterial = rendererPrincipal.sharedMaterial;

        chunks.Add(coordonnee, nouveauChunk);
    }

    private void AjouterChunkDirection(Vector2Int direction)
    {
        if (chunks.Count == 0) return;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minZ = int.MaxValue, maxZ = int.MinValue;

        foreach (Vector2Int coordonnee in chunks.Keys)
        {
            minX = Mathf.Min(minX, coordonnee.x);
            maxX = Mathf.Max(maxX, coordonnee.x);
            minZ = Mathf.Min(minZ, coordonnee.y);
            maxZ = Mathf.Max(maxZ, coordonnee.y);
        }

        if (direction == Vector2Int.up || direction == Vector2Int.down)
        {
            int z = direction == Vector2Int.up ? maxZ + 1 : minZ - 1;
            for (int x = minX; x <= maxX; x++) CreerChunk(new Vector2Int(x, z));
        }
        else
        {
            int x = direction == Vector2Int.right ? maxX + 1 : minX - 1;
            for (int z = minZ; z <= maxZ; z++) CreerChunk(new Vector2Int(x, z));
        }
    }

    private void SurlignerChunks()
    {
        RestaurerMateriauxChunks();
        int index = 0;

        foreach (KeyValuePair<Vector2Int, CreationSimpleTerrain> element in chunks)
        {
            MeshRenderer renderer = element.Value.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) continue;

            materiauxOriginaux[element.Value] = renderer.sharedMaterial;
            Material materiau = new Material(renderer.sharedMaterial);
            materiau.color = Color.HSVToRGB((index % 8) / 8f, 0.75f, 1f);
            renderer.material = materiau;
            index++;
        }

        tempsSurlignageChunks = dureeSurlignageChunks;
    }

    private void RestaurerMateriauxChunks()
    {
        foreach (KeyValuePair<CreationSimpleTerrain, Material> element in materiauxOriginaux)
        {
            if (element.Key == null) continue;
            MeshRenderer renderer = element.Key.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = element.Value;
        }
        materiauxOriginaux.Clear();
    }

    public void AppliquerDeformationDepuisMonde(Vector3 positionMonde, float rayon)
    {
        if (p_vertices == null || p_mesh == null) return;

        Vector3 positionLocale = transform.InverseTransformPoint(positionMonde);
        float sigma = Mathf.Max(0.01f, rayon / 2f);
        float sigmaCarre = sigma * sigma;
        bool modification = false;

        for (int i = 0; i < p_vertices.Length; i++)
        {
            float dx = p_vertices[i].x - positionLocale.x;
            float dz = p_vertices[i].z - positionLocale.z;
            float distanceCarre = dx * dx + dz * dz;

            if (distanceCarre <= rayon * rayon)
            {
                float valeurGaussienne = Mathf.Exp(-distanceCarre / (2f * sigmaCarre));
                p_vertices[i].y += 20f * valeurGaussienne;
                modification = true;
            }
        }

        if (!modification) return;

        p_mesh.SetVertices(p_vertices);
        for (uint i = 0; i < p_vertices.Length; i++) calculerNormaleVertex(i);
        p_mesh.SetNormals(p_normals);

        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = p_mesh;
        }

        mettreAJourLODApresDeformation();
    }

    private void mettreAJourLODApresDeformation()
    {
        Transform lod1Transform = transform.Find("LOD1");
        Transform lod2Transform = transform.Find("LOD2");
        if (lod1Transform == null || lod2Transform == null) return;

        MeshFilter meshFilterLOD1 = lod1Transform.GetComponent<MeshFilter>();
        MeshFilter meshFilterLOD2 = lod2Transform.GetComponent<MeshFilter>();

        if (meshFilterLOD1 != null)
        {
            int resolutionLOD1 = Mathf.Max(32, (int)Mathf.Pow(2, Mathf.Max(4, puissance2Resolution - 3)));
            p_meshLOD1 = creerMeshLOD(resolutionLOD1);
            meshFilterLOD1.mesh = p_meshLOD1;
        }

        if (meshFilterLOD2 != null)
        {
            p_meshLOD2 = creerMeshLOD(16);
            meshFilterLOD2.mesh = p_meshLOD2;
        }
    }

    private void GererExercice5()
    {
        if (!estChunkPrincipal) return;

        if (Keyboard.current.upArrowKey.wasPressedThisFrame) AjouterChunkDirection(Vector2Int.up);
        if (Keyboard.current.downArrowKey.wasPressedThisFrame) AjouterChunkDirection(Vector2Int.down);
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame) AjouterChunkDirection(Vector2Int.left);
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame) AjouterChunkDirection(Vector2Int.right);
        if (Keyboard.current.cKey.wasPressedThisFrame) SurlignerChunks();

        if (tempsSurlignageChunks > 0f)
        {
            tempsSurlignageChunks -= Time.deltaTime;
            if (tempsSurlignageChunks <= 0f) RestaurerMateriauxChunks();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray rayon = p_cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;

            if (Physics.Raycast(rayon, out hit, Mathf.Infinity, maskPickingTerrain))
            {
                // On enregistre la déformation dans l'espace monde.
                // Tous les chunks utilisent ensuite le même centre et le même rayon.
                historiqueDeformations.Add(new DeformationEnregistree(hit.point, rayonDeformationChunk));

                foreach (CreationSimpleTerrain chunk in chunks.Values)
                    chunk.AppliquerDeformationDepuisMonde(hit.point, rayonDeformationChunk);
            }
        }
    }



    void OnGUI()

    {

        if (afficherAide)

        {

            GUILayout.BeginArea(new Rect(10, 10, 500, 500), GUI.skin.box);



            GUILayout.Label("=== INFORMATIONS DU TERRAIN ===");

            GUILayout.Label("Nombre de sommets : " + p_dimVertices);

            GUILayout.Label("Nombre de triangles : " + p_dimTriangles);



            GUILayout.Label("Mode de normale : " + typeNormale);



            GUILayout.Space(10);



            GUILayout.Label("=== COMMANDES ===");

            GUILayout.Label("F1 : Afficher / cacher l'aide");

            GUILayout.Label("F2 : Changer la fonction de déformation");

            GUILayout.Label("F3 : Changer la HeightMap");

            GUILayout.Label("F10 : Afficher les normales");

            GUILayout.Label("F12 : Changer le mode de normale");

            GUILayout.Label("Clic gauche : Déformer le terrain");

            GUILayout.Label("Clic droit : Tourner le terrain");

            GUILayout.Label("ZQSD : Déplacer la caméra");



            GUILayout.Label("=== FPS ===");

            GUILayout.Label("FPS : " + fps.ToString("F1"));



            GUILayout.EndArea();

        }

    }



    private void afficherNormales()

    {

        for (int i = 0; i < p_triangles.Length; i += 3)

        {

            int indexA = p_triangles[i];

            int indexB = p_triangles[i + 1];

            int indexC = p_triangles[i + 2];



            Vector3 A = p_vertices[indexA];

            Vector3 B = p_vertices[indexB];

            Vector3 C = p_vertices[indexC];



            Vector3 centre = (A + B + C) / 3f;



            Vector3 v01 = B - A;

            Vector3 v02 = C - A;



            Vector3 normaleOrientation = Vector3.Cross(v01, v02).normalized;



            Vector3 normaleEclairage = (

                p_normals[indexA] +

                p_normals[indexB] +

                p_normals[indexC]

            ).normalized;



            if (typeAffichageNormale == TypeAffichageNormale.Vertices)

            {

                Debug.DrawRay(

                    transform.position + A,

                    p_normals[indexA].normalized * 2f,

                    Color.red,

                    3f,

                    false

                );



                Debug.DrawRay(

                    transform.position + B,

                    p_normals[indexB].normalized * 2f,

                    Color.red,

                    3f,

                    false

                );



                Debug.DrawRay(

                    transform.position + C,

                    p_normals[indexC].normalized * 2f,

                    Color.red,

                    3f,

                    false

                );

            }



            if (typeAffichageNormale == TypeAffichageNormale.Eclairage ||

                typeAffichageNormale == TypeAffichageNormale.EclairageOrientation)

            {

                Debug.DrawRay(

                    transform.position + centre,

                    normaleEclairage * 2f,

                    Color.green,

                    3f,

                    false

                );

            }



            if (typeAffichageNormale == TypeAffichageNormale.Orientation ||

                typeAffichageNormale == TypeAffichageNormale.EclairageOrientation)

            {

                Debug.DrawRay(

                    transform.position + centre,

                    normaleOrientation * 3f,

                    Color.yellow,

                    3f,

                    false

                );

            }

        }

    }



    void Start()

    {

        creerLeMeshTerrain();

        if (estChunkPrincipal)

        {

            chunks.Clear();
            chunks.Add(Vector2Int.zero, this);
            gameObject.name = "Chunk_0_0";

        }
        else if (historiqueDeformations != null)
        {
            // Le mesh du nouveau chunk existe maintenant.
            // On rejoue uniquement les déformations historiques qui
            // touchent réellement ses sommets.
            foreach (DeformationEnregistree deformation in historiqueDeformations)
                AppliquerDeformationDepuisMonde(deformation.position, deformation.rayon);
        }

    }



    void Update()
    {
        fps = 1.0f / Time.deltaTime;

        if (estChunkPrincipal)
        {
            if (Keyboard.current.f10Key.wasPressedThisFrame)
            {
                typeAffichageNormale = (TypeAffichageNormale)(((int)typeAffichageNormale + 1) % 4);
                tempsAffichageNormales = 3f;
            }

            if (tempsAffichageNormales > 0)
            {
                tempsAffichageNormales -= Time.deltaTime;
                afficherNormales();
            }

            if (Keyboard.current.f1Key.wasPressedThisFrame)
                afficherAide = !afficherAide;

            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                choixModeDeformation = ChoixModeDeformation.Fonction;
                appliquerDeformation_Fonction();
                typeFonction = (TypeFonction)(((int)typeFonction + 1) % 3);
                foreach (CreationSimpleTerrain chunk in chunks.Values)
                    chunk.mettreAJourLODApresDeformation();
            }

            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                choixModeDeformation = ChoixModeDeformation.Texture;
                if (textures != null && textures.Count > 0)
                {
                    appliquerDeformation_Texture();
                    numTexture = (numTexture + 1) % textures.Count;
                }
            }

            if (Keyboard.current.f12Key.wasPressedThisFrame)
            {
                typeNormale = (TypeNormale)(((int)typeNormale + 1) % 3);
                for (uint i = 0; i < p_vertices.Length; i++) calculerNormaleVertex(i);
                p_mesh.SetNormals(p_normals);
            }

            GererExercice5();
        }

        deplacerCamera();
    }

}