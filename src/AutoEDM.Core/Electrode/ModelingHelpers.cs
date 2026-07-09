using System;
using System.Linq;
using System.Reflection;
using AutoEDM.Diagnostics;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Helpers late-bound para modelagem no Solid Edge. Toda a geometria é criada
    /// via COM dinâmico, usando InvokeMember quando o binder do C# falha na coerção
    /// de VARIANT.
    /// </summary>
    internal static class ModelingHelpers
    {
        /// <summary>
        /// Cria um bloco (protrusão) por dois pontos em um plano de referência.
        /// Unidades: metros.
        /// </summary>
        internal static dynamic AddBoxByTwoPoints(dynamic models,
            double x1, double y1, double z1,
            double x2, double y2, double z2,
            double depth,
            dynamic refPlane,
            int extentSide = 1, // igLeft
            bool keyPointExtent = false)
        {
            object[] args =
            {
                x1, y1, z1,
                x2, y2, z2,
                0.0,          // dAngle
                depth,        // dDepth
                refPlane,     // pPlane
                extentSide,
                keyPointExtent,
                null,         // pKeyPointObj
                0             // pKeyPointFlags
            };

            return InvokeMethod(models, "AddBoxByTwoPoints", args);
        }

        /// <summary>
        /// Aplica FaceOffset em um conjunto de faces. Unidades: metros (offset).
        /// </summary>
        internal static dynamic AddFaceOffset(dynamic constructions, object[] faces, double offsetMeters)
        {
            // Assinatura (dump): Add(FacesToOffset, BlendRecreation, AlongOrReverseVector,
            //   offsetDistance, ToReferenceEntity, ToKeyPoint, DistanceFromKeyPoint,
            //   AlongOrReverseDirectionToKeyPoint) [8 params]
            object[] args =
            {
                faces,          // FacesToOffset (array de faces)
                0,              // BlendRecreation (0 = sem recriação, validar)
                0,              // AlongOrReverseVector (0 = padrão, validar)
                offsetMeters,   // offsetDistance
                Type.Missing,   // ToReferenceEntity
                Type.Missing,   // ToKeyPoint
                Type.Missing,   // DistanceFromKeyPoint
                Type.Missing    // AlongOrReverseDirectionToKeyPoint
            };

            return InvokeMethod(constructions.FaceOffsets, "Add", args);
        }

        /// <summary>
        /// Aplica OffsetSurface em um conjunto de faces (alternativa ao FaceOffset).
        /// </summary>
        internal static dynamic AddOffsetSurface(dynamic constructions, object[] faces, double offsetMeters)
        {
            // Assinatura (dump): Add(Side, offsetDistance, FaceSet, Boundary) [4 params]
            // FaceSet pode aceitar array de faces diretamente.
            object[] args =
            {
                0,              // Side
                offsetMeters,   // offsetDistance
                faces,          // FaceSet
                Type.Missing    // Boundary
            };

            return InvokeMethod(constructions.OffsetSurfaces, "Add", args);
        }

        /// <summary>
        /// Une superfícies offsetadas/copiadas em um sólido via StitchSurfaces.
        /// </summary>
        internal static dynamic StitchSurfaces(dynamic constructions, object[] surfaces, bool heal = true, double toleranceMeters = 0.0001)
        {
            object[] args =
            {
                surfaces.Length,
                surfaces,
                heal,
                toleranceMeters
            };

            return InvokeMethod(constructions.StitchSurfaces, "Add", args);
        }

        /// <summary>
        /// Pinta um conjunto de faces com a cor indicada (RGB 0..255).
        /// A API COM do Solid Edge armazena cores como float 0..1.
        /// </summary>
        internal static void SetFacesColor(dynamic faces, byte r, byte g, byte b)
        {
            foreach (var face in faces)
            {
                try
                {
                    dynamic style = face.Style;
                    if (style == null) continue;
                    style.DiffuseRed = r / 255.0;
                    style.DiffuseGreen = g / 255.0;
                    style.DiffuseBlue = b / 255.0;
                }
                catch (Exception ex)
                {
                    Log.Warn($"Não foi possível pintar face: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Tenta obter o plano de referência base (RefPlanes.Item(1)) de um PartDocument.
        /// </summary>
        internal static dynamic GetBaseRefPlane(dynamic partDocument)
        {
            return InvokeGet(partDocument.RefPlanes, "Item", 1);
        }

        /// <summary>
        /// Obtém as faces de uma feature (ex.: CopySurface, OffsetSurface) como array.
        /// </summary>
        internal static object[] GetFeatureFaces(dynamic feature)
        {
            try
            {
                dynamic faces = feature.Faces(0); // 0 = all faces?
                int count = faces.Count;
                var list = new object[count];
                for (int i = 1; i <= count; i++)
                    list[i - 1] = faces.Item(i);
                return list;
            }
            catch (Exception ex)
            {
                Log.Warn($"Não foi possível obter faces da feature: {ex.Message}");
                return new object[0];
            }
        }

        // ---------------------------------------------------------------------

        private static dynamic InvokeMethod(object target, string methodName, object[] args)
        {
            try
            {
                return target.GetType().InvokeMember(
                    methodName,
                    BindingFlags.InvokeMethod,
                    null,
                    target,
                    args);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw tie.InnerException;
            }
        }

        private static dynamic InvokeGet(object target, string methodName, object arg)
        {
            return target.GetType().InvokeMember(
                methodName,
                BindingFlags.GetProperty,
                null,
                target,
                new[] { arg });
        }
    }
}
