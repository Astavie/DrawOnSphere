using Godot;

public interface Projection {

    Image create(int size);

    void draw(Image image, Vector3 point, Color color, int radius);

    void drawLine(Image image, Vector3 point1, Vector3 point2, Color color, int radius);

}

public class ProjectionEquirectangular : Projection {

    public Image create(int size) {
        Image image = new Image();
        image.Create(size * 2, size, false, Image.Format.Rgba8);
        return image;
    }

    private Vector2 getPoint(Image image, Vector3 point) {
        float u = Mathf.Atan2(point.x, point.z) / (2 * Mathf.Pi);
        float v = 0.5f - Mathf.Asin(point.y) / Mathf.Pi;

        if (u < 0)
            u += 1;

        int x = (int) (u * image.GetWidth());
        int y = (int) (v * image.GetHeight());

        return new Vector2(x, y);
    }

    public void draw(Image image, Vector3 point, Color color, int radius) {
        Vector2 xy = getPoint(image, point);
        image.Lock();
        draw(image, (int) xy.x, (int) xy.y, radius, color);
        image.Unlock();
    }

    private void draw(Image image, int cx, int cy, int radius, Color color) {
        for (int y = cy - Mathf.FloorToInt(radius / 2f); y < cy + Mathf.CeilToInt(radius / 2f); y++) {
            if (y < 0 || y >= image.GetHeight())
                continue;

            for (int x = cx - Mathf.FloorToInt(radius / 2f); x < cx + Mathf.CeilToInt(radius / 2f); x++) {
                float dx = cx - x;
                float dy = cy - y;
                if (radius % 2 == 0) {
                    dx -= 0.5f;
                    dy -= 0.5f;
                }
                if ((dx * dx + dy * dy) > Mathf.FloorToInt((radius / 2f) * (radius / 2f) - 0.25f))
                    continue;

                int rx = x;

                if (rx < 0)
                    rx += image.GetWidth();
                else if (rx >= image.GetWidth())
                    rx -= image.GetWidth();
                
                image.SetPixel(rx, y, color);
            }
        }
    }

    public void drawLine(Image image, Vector3 point1, Vector3 point2, Color color, int radius) {
        Vector2 before = getPoint(image, point1);
        Vector2 after = getPoint(image, point2);

        // Wrap around
        if (before.x > after.x && (before.x - after.x) > image.GetHeight()) {
            after += new Vector2(image.GetWidth(), 0);
        } else if (after.x > before.x && (after.x - before.x) > image.GetHeight()) {
            before += new Vector2(image.GetWidth(), 0);
        }

        float deltax = after.x - before.x;
        float deltay = after.y - before.y;

        if (Mathf.Abs(deltax) > Mathf.Abs(deltay)) {
            if (before.x > after.x) {
                Vector2 tmp = after;
                after = before;
                before = tmp;

                deltay = -deltay;
            }

            float deltaerr = Mathf.Abs(deltay / deltax);
            float error = 0;

            int y = (int) before.y;

            image.Lock();
            for (int x = (int) before.x; x <= after.x; x++) {
                draw(image, x % image.GetWidth(), y, radius, color);
                error += deltaerr;
                if (error >= 0.5f) {
                    y += Mathf.Sign(deltay);
                    error -= 1;
                }
            }
            image.Unlock();
        } else {
            if (before.y > after.y) {
                Vector2 tmp = after;
                after = before;
                before = tmp;
                
                deltax = -deltax;
            }

            float deltaerr = Mathf.Abs(deltax / deltay);
            float error = 0;

            int x = (int) before.x;

            image.Lock();
            for (int y = (int) before.y; y <= after.y; y++) {
                draw(image, x % image.GetWidth(), y, radius, color);
                error += deltaerr;
                if (error >= 0.5f) {
                    x += Mathf.Sign(deltax);
                    error -= 1;
                }
            }
            image.Unlock();
        }
    }

}
