using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Algorithms;

/// <summary>
/// Utilities for cropping images using various bounding shapes.
/// </summary>
public static class ImageCropping
{
    /// <summary>
    /// Crops an image using an axis-aligned rectangle.
    /// </summary>
    /// <param name="image">Source image to crop</param>
    /// <param name="rectangle">Axis-aligned rectangle defining the crop area</param>
    /// <returns>Cropped image</returns>
    public static Image<Rgb24> CropAxisAligned(Image<Rgb24> image, Rectangle rectangle)
    {
        return image.Clone(x => x.Crop(rectangle));
    }

    /// <summary>
    /// Crops an image using an oriented rectangle defined by four corner points.
    /// The cropping preserves text orientation by ensuring proper mapping from
    /// the oriented rectangle to the output image.
    ///
    /// Assumptions about text orientation:
    /// - The oriented rectangle vertices are provided in order: top-left, top-right, bottom-right, bottom-left
    /// - Text flows from left to right along the top edge (from vertex 0 to vertex 1)
    /// - Text reading direction follows the "width" of the rectangle (top edge direction)
    /// </summary>
    /// <param name="image">Source image to crop</param>
    /// <param name="orientedRectangle">Four corner points defining the oriented rectangle</param>
    /// <returns>Cropped and rectified image where text appears upright</returns>
    /// <exception cref="ArgumentException">Thrown when orientedRectangle doesn't have exactly 4 points</exception>
    public static Image<Rgb24> CropOriented(Image<Rgb24> image, List<(int X, int Y)> orientedRectangle)
    {
        if (orientedRectangle == null || orientedRectangle.Count != 4)
            throw new ArgumentException("Oriented rectangle must have exactly 4 points", nameof(orientedRectangle));

        // TODO: Implement oriented rectangle cropping with perspective transformation
        // For now, fall back to axis-aligned cropping using the bounding box of the oriented rectangle
        var aaRect = BoundingRectangles.ComputeAxisAlignedRectangle(orientedRectangle);
        return CropAxisAligned(image, aaRect);
    }
}
