﻿module BitonalDocs.Imaging

open System
open System.Drawing
open System.Drawing.Imaging
open System.Runtime.InteropServices
open BitonalDocs.Dithering

//-------------------------------------------------------------------------------------------------

let private pixelFormat = PixelFormat.Format32bppArgb

let private convertImageToColorArray (image : Bitmap) =

    let rows = image.Height
    let cols = image.Width

    let rect = Rectangle(Point.Empty, image.Size)
    let data = image.LockBits(rect, ImageLockMode.ReadOnly, pixelFormat)
    let byteCount = Math.Abs(data.Stride) * rows
    let bytes = Array.zeroCreate<byte> byteCount
    Marshal.Copy(data.Scan0, bytes, 0, byteCount)
    image.UnlockBits(data)

    let computeValue row col =
        let i = ((row * cols) + col) * 4
        let r = bytes.[i + 2]
        let g = bytes.[i + 1]
        let b = bytes.[i + 0]
        Color(r, g, b)

    Array2D.init rows cols computeValue

let private convertTo1Bpp (image : Pixel[,]) =

    let rows = Array2D.length1 image
    let cols = Array2D.length2 image
    let stride = int (ceil (double cols / 8.0))

    let rec reduceBits offset acc = function
        | bits when bits = 0 -> acc
        | bits ->
            let x = (offset % cols) + bits - 1
            let y = (offset / cols)
            let pixel = match image.[y, x] with Black -> 1uy | White -> 0uy
            let value = acc ||| (pixel <<< (8 - bits))
            reduceBits offset value (bits - 1)

    let computeValue i =
        let offsetY = (i / stride) * cols
        let offsetX = (i % stride) * 8
        let offset = offsetY + offsetX
        let bits = Math.Min(8, cols - offsetX)
        reduceBits offset 0uy bits

    Array.init (stride * rows) computeValue

let createTiffImage w h resolution render =

    let resolution = single resolution
    let w = int (ceil (resolution * w))
    let h = int (ceil (resolution * h))

    use bitmap = new Bitmap(w, h, pixelFormat)
    bitmap.SetResolution(resolution, resolution)

    use graphics = Graphics.FromImage(bitmap)
    render graphics

    bitmap
    |> convertImageToColorArray
    |> dither (Threshold.fixed' 127uy)
    |> convertTo1Bpp
    |> Tiff.createImageFile (uint32 w) (uint32 h) (uint32 resolution)
    |> Tiff.serializeImageFile
