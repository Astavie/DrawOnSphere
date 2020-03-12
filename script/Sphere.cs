using Godot;
using System;

public class Sphere : MeshInstance
{

    private Image image, undo;

    public override void _Ready() {
        image = new Image();
        undo = new Image();

        image.Create(1024, 512, false, Image.Format.Rgb8);
        Create(image);

        ((MenuButton) GetNode(file)).GetPopup().Connect("id_pressed", this, "OnFilePressed");
        ((WindowDialog) GetNode(newGlobe)).Connect("confirmed", this, "OnFileNew");
        ((FileDialog) GetNode(saveGlobe)).Connect("file_selected", this, "OnFileSave");
        ((FileDialog) GetNode(openGlobe)).Connect("file_selected", this, "OnFileOpen");

        normalAnchor = ((Spatial) GetNode(anchor)).Transform;
        normalSize = ((Camera) GetNode(camera)).Size;
    }

    [Export]
    public NodePath color, camera, anchor, texture, size, file, newGlobe, saveGlobe, openGlobe;

    public Projection projection = new ProjectionEquirectangular();

    private Vector3? lastSelected = null;
    private string lastSave = null;

    private Transform normalAnchor;
    private float normalSize;

    public void OnFilePressed(int id) {
        switch(id) {
            case 0: // new
                WindowDialog dialog = (WindowDialog) GetNode(newGlobe);
                dialog.PopupCentered();
            break;
            case 1: // open
                FileDialog open = (FileDialog) GetNode(openGlobe);
                open.PopupCentered(); 
            break;
            case 3: // save
                if (lastSave != null) {
                    image.SavePng(lastSave);
                } else {
                    FileDialog save1 = (FileDialog) GetNode(saveGlobe);
                    save1.PopupCentered();
                }
            break;
            case 4: // save as
                FileDialog save2 = (FileDialog) GetNode(saveGlobe);
                save2.PopupCentered(); 
            break;
            case 6: // quit
                GetTree().Quit();
            break;
        }
    }

    public void OnFileNew() {
        WindowDialog dialog = (WindowDialog) GetNode(newGlobe);
        int size = (int) ((SpinBox) dialog.GetNode((NodePath)dialog.Get("size"))).Value;

        ((Spatial) GetNode(anchor)).Transform = normalAnchor;
        ((Camera) GetNode(camera)).Size = normalSize;

        image = new Image();
        undo = new Image();

        image.Create(size * 2, size, false, Image.Format.Rgb8);
        Create(image);
    }

    public void OnFileSave(string file) {
        image.SavePng(file);
        lastSave = file;
    }

    public void OnFileOpen(string file) {
        Image tmp = new Image();
        tmp.Load(file);
        if (tmp.GetWidth() != tmp.GetHeight() * 2) {
            OS.Alert("Error: Selected file is not equirectangular", "Could not open globe");
            return;
        }

        ((Spatial) GetNode(anchor)).Transform = normalAnchor;
        ((Camera) GetNode(camera)).Size = normalSize;

        image = tmp;
        undo = new Image();

        Create(image, true);
    }

    public void Create(Image image, bool open = false) {
        lastSave = null;

        if (!open) {
            image.Fill(new Color(.25f, .25f, .25f));

            image.Lock();
            for (int x = 0; x < 24; x++) {
                for (int y = 0; y < image.GetHeight(); y++) {
                    image.SetPixel((int) (image.GetWidth() / 24f * x), y, new Color(.5f, .5f, .5f));
                }
            }

            for (int y = 0; y < 12; y++) {
                for (int x = 0; x < image.GetWidth(); x++) {
                    image.SetPixel(x, (int) (image.GetHeight() / 12f * y), new Color(.5f, .5f, .5f));
                }
            }

            image.Unlock();
        }

        undo.CopyFrom(image);

        MaterialOverride = new SpatialMaterial();
        ((SpatialMaterial) MaterialOverride).AlbedoTexture = new ImageTexture();
        ((ImageTexture) ((SpatialMaterial) MaterialOverride).AlbedoTexture).CreateFromImage(image);

        ((TextureRect) GetNode(texture)).Texture = ((SpatialMaterial) MaterialOverride).AlbedoTexture;
    }

    public void Set(Image image) {
        ((ImageTexture) ((SpatialMaterial) MaterialOverride).AlbedoTexture).SetData(image);
    }

    public override void _Input(InputEvent @event) {
        if (((WindowDialog) GetNode(newGlobe)).Visible)
            return;
        if (((WindowDialog) GetNode(openGlobe)).Visible)
            return;
        if (((WindowDialog) GetNode(saveGlobe)).Visible)
            return;

        if (@event is InputEventMouseButton) {
            InputEventMouseButton mouse = (InputEventMouseButton) @event;

            // Get if mouse is within viewport
            if (mouse.Position.x > 0 && mouse.Position.y > 0 && mouse.Position < GetViewport().Size) {

                // Get if mouse is left press
                if (mouse.Pressed && mouse.ButtonIndex == (int) Godot.ButtonList.Left) {

                    // Prepare for undo
                    undo.CopyFrom(image);

                    Color color = ((ColorPicker) GetNode(this.color)).Color;

                    Vector3? point = GetOnUnitSphere(mouse.Position);
                    if (point.HasValue) {
                        projection.draw(image, point.Value, color, (int) ((SpinBox) GetNode(size)).Value);
                        Set(image);
                    }

                    lastSelected = point;
                }

                if (mouse.ButtonIndex == (int) Godot.ButtonList.WheelUp) {
                    ((Camera) GetNode(camera)).Size /= 1.1f;
                }

                if (mouse.ButtonIndex == (int) Godot.ButtonList.WheelDown) {
                    ((Camera) GetNode(camera)).Size *= 1.1f;
                }
            }
        }

        if (@event is InputEventMouseMotion) {
            InputEventMouseMotion mouse = (InputEventMouseMotion) @event;

            // Get if mouse is within viewport
            if (mouse.Position.x > 0 && mouse.Position.y > 0 && mouse.Position < GetViewport().Size) {
                
                // Get if mouse is left press
                if (Input.IsMouseButtonPressed((int) Godot.ButtonList.Left)) {
                    Color color = ((ColorPicker) GetNode(this.color)).Color;

                    Vector3? before = lastSelected;
                    Vector3? after = GetOnUnitSphere(mouse.Position);
                    
                    if (before.HasValue && after.HasValue) {
                        projection.drawLine(image, before.Value, after.Value, color, (int) ((SpinBox) GetNode(size)).Value);
                        Set(image);
                    }

                    lastSelected = after;
                }

                // Get if mouse is right press
                if (Input.IsMouseButtonPressed((int) Godot.ButtonList.Right)) {
                    Vector2 relative = mouse.Relative;

                    Camera camera = GetViewport().GetCamera();
                    float dis = camera.ProjectRayOrigin(Vector2.Zero).DistanceTo(camera.ProjectRayOrigin(relative));

                    Vector2 move = relative.Normalized() * dis;

                    Spatial spatial = (Spatial) GetNode(anchor);

                    spatial.Rotation = new Vector3(spatial.Rotation.x - move.y, spatial.Rotation.y - move.x, 0);
                }
            }
        }

        if (@event.IsActionPressed("undo")) {
            doUndo();
        }

        if (@event.IsActionPressed("filesaveas")) {
            OnFilePressed(4);
        } else if (@event.IsActionPressed("filesave")) {
            OnFilePressed(3);
        } else if (@event.IsActionPressed("fileopen")) {
            OnFilePressed(1);
        } else if (@event.IsActionPressed("filenew")) {
            OnFilePressed(0);
        } else if (@event.IsActionPressed("quit")) {
            OnFilePressed(6);
        }
    }

    public void doUndo() {
        Image tmp = new Image();
        tmp.CopyFrom(image);

        image.CopyFrom(undo);
        undo.CopyFrom(tmp);

        Set(image);
    }

    private Vector3? GetOnUnitSphere(Vector2 point) {
        Camera camera = GetViewport().GetCamera();
        Vector3 origin = camera.ProjectRayOrigin(point);
        Vector3 normal = camera.ProjectRayNormal(point);

        // ABC-formula

        float a = 1;
        float b = 2 * (normal.Dot(origin));
        float c = origin.LengthSquared() - 1;

        float d = (-b - Mathf.Sqrt(b * b - 4 * a * c)) / (2 * a);

        if (float.IsNaN(d))
            return null;

        return origin + d * normal;
    }

}
