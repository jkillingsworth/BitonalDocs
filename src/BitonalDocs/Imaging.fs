﻿module BitonalDocs.Imaging

open System
open System.Drawing
open System.Drawing.Imaging
open System.Runtime.InteropServices

//-------------------------------------------------------------------------------------------------

let private pixelFormat = PixelFormat.Format32bppArgb

let private convertImageToColorArray (image : Bitmap) =

    let w = image.Width
    let h = image.Height

    let rect = Rectangle(Point.Empty, image.Size)
    let data = image.LockBits(rect, ImageLockMode.ReadOnly, pixelFormat)
    let byteCount = Math.Abs(data.Stride) * h
    let bytes = Array.zeroCreate<byte> byteCount
    Marshal.Copy(data.Scan0, bytes, 0, byteCount)
    image.UnlockBits(data)

    let computeValue x y =
        let i = ((y * w) + x) * 4
        let r = bytes.[i + 2]
        let g = bytes.[i + 1]
        let b = bytes.[i + 0]
        Dithering.Color(r, g, b)

    Array2D.init w h computeValue

let private convertTo1Bpp (image : bool[,]) =

    let w = Array2D.length1 image
    let h = Array2D.length2 image
    let stride = int (Math.Ceiling(float w / 8.0))

    let rec reduceBits offset acc = function
        | bits when bits = 0 -> acc
        | bits ->
            let x = (offset % w) + bits - 1
            let y = (offset / w)
            let pixel = match image.[x, y] with true -> 0uy | false -> 1uy
            let value = acc ||| (pixel <<< (8 - bits))
            reduceBits offset value (bits - 1)

    let computeValue i =
        let offsetY = (i / stride) * w
        let offsetX = (i % stride) * 8
        let offset = offsetY + offsetX
        let bits = Math.Min(8, w - offsetX)
        reduceBits offset 0uy bits

    Array.Parallel.init (stride * h) computeValue

let createTiffImage w h resolution render =

    let resolution = float32 resolution
    let w = int (Math.Ceiling(float (resolution * w)))
    let h = int (Math.Ceiling(float (resolution * h)))

    use bitmap = new Bitmap(w, h, pixelFormat)
    bitmap.SetResolution(resolution, resolution)

    use graphics = Graphics.FromImage(bitmap)
    render graphics

    bitmap
    |> convertImageToColorArray
    |> Dithering.dither (Dithering.Threshold.fixed' 127uy)
    |> convertTo1Bpp
    |> Tiff.createImageFile (uint32 w) (uint32 h) (uint32 resolution)
    |> Tiff.serializeImageFile
