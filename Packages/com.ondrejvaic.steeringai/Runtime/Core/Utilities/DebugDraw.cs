using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

#if STEERING_DEBUG
namespace SteeringAI.Core
{
    public class DebugDraw
    {
        public static void DrawArrow(Vector3 from, Vector3 to, Color color, float duration = 0)
        {
            Debug.DrawLine(from, to, color, duration);

            Vector3 direction = to - from;
            if(Vector3.SqrMagnitude(direction) < math.EPSILON) return;
            
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 30, 0) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 30, 0) * Vector3.forward;

            Debug.DrawRay(to, right * 0.15f, color, duration);
            Debug.DrawRay(to, left * 0.15f, color, duration);
        }
        
        public static void DrawCircleXZ(Vector3 center, float radius, Color color, int numPoints = 24, float duration = 0)
        {
            float angleIncrement = 360.0f / numPoints;

            for (int i = 0; i < numPoints; i++)
            {
                float angleA = i * angleIncrement;
                float angleB = (i + 1) * angleIncrement;

                Vector3 pointA = center + Quaternion.Euler(0, angleA, 0) * Vector3.forward * radius;
                Vector3 pointB = center + Quaternion.Euler(0, angleB, 0) * Vector3.forward * radius;

                Debug.DrawLine(pointA, pointB, color, duration);
            }
        }
        
        public static void DrawArcXZ(Vector3 center, Vector3 direction, float radius, float angle, Color color, int numPoints = 24, float duration = 0)
        {
            direction = new Vector3(direction.x, 0, direction.z).normalized;
            
            float angleIncrement = angle / numPoints;
            float halfMaxAngle = angle / 2; 
            for (int i = 0; i < numPoints; i++)
            {
                float angleA = i * angleIncrement;
                float angleB = (i + 1) * angleIncrement;

                Vector3 pointA = center + Quaternion.Euler(0, angleA - halfMaxAngle, 0) * direction * radius;
                Vector3 pointB = center + Quaternion.Euler(0, angleB - halfMaxAngle, 0) * direction * radius;

                Debug.DrawLine(pointA, pointB, color, duration);

                if (halfMaxAngle < 180 - 0.01)
                {
                    if(i == 0)
                    {
                        Debug.DrawLine(center, pointA, color, duration);
                    }
                    if(i == numPoints - 1)
                    {
                        Debug.DrawLine(center, pointB, color, duration);
                    }   
                }
            }
        }
        
        public static void DrawCircleXY(Vector3 center, float radius, Color color, int numPoints = 24, float duration = 0)
        {
            float angleIncrement = 360.0f / numPoints;

            for (int i = 0; i < numPoints; i++)
            {
                float angleA = i * angleIncrement;
                float angleB = (i + 1) * angleIncrement;

                Vector3 pointA = center + Quaternion.Euler(0, 0, angleA) * Vector3.up * radius;
                Vector3 pointB = center + Quaternion.Euler(0, 0, angleB) * Vector3.up * radius;

                Debug.DrawLine(pointA, pointB, color, duration);
            }
        }
        
        public static void DrawCircleYZ(Vector3 center, float radius, Color color, int numPoints = 24, float duration = 0)
        {
            float angleIncrement = 360.0f / numPoints;

            for (int i = 0; i < numPoints; i++)
            {
                float angleA = i * angleIncrement;
                float angleB = (i + 1) * angleIncrement;

                Vector3 pointA = center + Quaternion.Euler(angleA, 0, 0) * Vector3.forward * radius;
                Vector3 pointB = center + Quaternion.Euler(angleB, 0, 0) * Vector3.forward * radius;

                Debug.DrawLine(pointA, pointB, color, duration);
            }
        }

        public static void DrawSphere(Vector3 center, float radius, Color color, int numPoints = 24, float duration = 0)
        {
			DrawCircleXZ(center, radius, color, numPoints, duration);
			DrawCircleXY(center, radius, color, numPoints, duration);
			DrawCircleYZ(center, radius, color, numPoints, duration);
        }

        public static float3 DrawNumber(float3 position, double num, int numInts, int numFracts, float scale, float spacing, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 currentPosition = position;
            
            if (num < 0)
            {
                num = -num;
                DrawMinus(currentPosition, scale, color);
                currentPosition += right * (spacing + scale);
            }
            
            int integerPart = (int)num;
            double fractionalPart = num - integerPart;
            NativeArray<int> integers = intToDigits(integerPart);
            NativeArray<int> fractions = fracToDigits(fractionalPart, numFracts);
            
            int drawInts = math.min(numInts, integers.Length);
            for (int i = 0; i < drawInts; i++)
            {
                int digit = integers[i];
                DrawNumberSingle(currentPosition, digit, scale, color);
                currentPosition += right * (scale + spacing);
            }
            currentPosition = DrawDot(currentPosition, scale, spacing, color);
            
            for (int i = 0; i < numFracts; i++)
            {
                int digit = fractions[i];
                DrawNumberSingle(currentPosition, digit, scale, color);
                currentPosition += right * (scale + spacing);
            }
            
            integers.Dispose();
            fractions.Dispose();
            return currentPosition;
        }

        public static void DrawBox(float3 topLeft, float3 bottomRight, float spacing, Color color)
        {
            float3 bottomLeft = new float3(topLeft.x, topLeft.y, bottomRight.z);
            float3 topRight = new float3(bottomRight.x, bottomRight.y, topLeft.z);
            
            Debug.DrawLine(
                topLeft + new float3(-1, 0, 1) * spacing,
                bottomLeft + new float3(-1, 0, -1) * spacing,
                color);
            
            Debug.DrawLine(
                topLeft + new float3(-1, 0, 1) * spacing,
                topRight + new float3(1, 0, 1) * spacing,
                color);
            
            Debug.DrawLine(
                bottomRight + new float3(1, 0, -1) * spacing,
                topRight + new float3(1, 0, 1) * spacing,
                color);
            
            Debug.DrawLine(
                bottomRight + new float3(1, 0, -1) * spacing,
                bottomLeft + new float3(-1, 0, -1) * spacing,
                color);
        }

        private static void DrawMinus(float3 position, float scale, Color color)
        {
            Debug.DrawLine(position + new float3(0, 0, 1) * scale, position + new float3(1, 0, 1) * scale, color);
        }

        public static float3 DrawDot(float3 position, float scale, float spacing, Color color)
        {
            float dotScale = DotSize * scale;
            Debug.DrawLine(position, position + new float3(1, 0, 0) * dotScale, color);
            Debug.DrawLine(position, position + new float3(0, 0, 1) * dotScale, color);
            Debug.DrawLine(position + new float3(1, 0, 1) * dotScale, position + new float3(1, 0, 0) * dotScale, color);
            Debug.DrawLine(position + new float3(1, 0, 1) * dotScale, position + new float3(0, 0, 1) * dotScale, color);

            return position + new float3(1, 0, 0) * (spacing + dotScale);
        }
        
        public static float3 DrawComma(float3 position, float scale, float spacing, Color color)
        {
            float dotScale = DotSize * scale;
            Debug.DrawLine(position + new float3(0, 0, -1) * dotScale, position + new float3(1, 0, 0) * dotScale, color);
            Debug.DrawLine(position + new float3(0, 0, -1) * dotScale, position + new float3(0, 0, 1) * dotScale, color);
            
            Debug.DrawLine(position + new float3(1, 0, 1) * dotScale, position + new float3(1, 0, 0) * dotScale, color);
            Debug.DrawLine(position + new float3(1, 0, 1) * dotScale, position + new float3(0, 0, 1) * dotScale, color);
            
            return position + new float3(1, 0, 0) * (spacing + dotScale + scale * 0.5f);
        }
        
        private static NativeArray<int> fracToDigits(double fractional, int numDigits)
        {
            var digits = new NativeArray<int>(numDigits, Allocator.Temp); // largest int has 10 digits

            for (int i = 0; i < numDigits; i++)
            {
                fractional *= 10;
                int digit = (int)(fractional);
                fractional -= digit;
                digits[i] = digit;
            }
            
            return digits;
        }

        private static NativeArray<int> intToDigits(int num)
        {
            var digits = new NativeArray<int>(10, Allocator.Temp); // largest int has 10 digits

            int index = -1;
            do
            {
                index++;
                int digit = num % 10;
                num /= 10;
                digits[index] = digit;
            } while (num != 0);

            var finalDigits = new NativeArray<int>(index + 1, Allocator.Temp);
            for (int i = 0; i < index + 1; i++)
            {
                finalDigits[i] = digits[index - i];
            }

            digits.Dispose();
            return finalDigits;
        } 
            
        private static void DrawNumberSingle(float3 position, float num, float scale, Color color)
        {
            switch (num)
            {
                case 0:
                    DrawZero(position, scale, color);
                    break;
                case 1:
                    DrawOne(position, scale, color);
                    break;
                case 2:
                    DrawTwo(position, scale, color);
                    break;
                case 3:
                    DrawThree(position, scale, color);
                    break;
                case 4:
                    DrawFour(position, scale, color);
                    break;
                case 5:
                    DrawFive(position, scale, color);
                    break;
                case 6:
                    DrawSix(position, scale, color);
                    break;
                case 7:
                    DrawSeven(position, scale, color);
                    break;
                case 8:
                    DrawEight(position, scale, color);
                    break;
                case 9:
                    DrawNine(position, scale, color);
                    break;
            }
        }
        
        private static void DrawZero(float3 position, float scale, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            float3 midLeft = bottomLeft + forward * scale;
            float3 topLeft = bottomLeft + forward * scale * 2;
            
            float3 bottomRight = bottomLeft + right * scale;
            float3 midRight = bottomRight + forward * scale;
            float3 topRight = bottomRight + forward * scale * 2;
            
            Debug.DrawLine(bottomLeft, midLeft, color);
            Debug.DrawLine(midLeft, topLeft, color);
            
            Debug.DrawLine(bottomRight, midRight, color);
            Debug.DrawLine(midRight, topRight, color);
            
            Debug.DrawLine(topLeft, topRight, color);
            Debug.DrawLine(bottomLeft, bottomRight, color);
        }
        
        private static void DrawOne(float3 position, float scale, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            
            float3 bottomRight = bottomLeft + right * scale;
            float3 midRight = bottomRight + forward * scale;
            float3 topRight = bottomRight + forward * scale * 2;
            float3 midLeft = bottomLeft + forward * scale;
            
            Debug.DrawLine(bottomRight, midRight, color);
            Debug.DrawLine(midRight, topRight, color);
            Debug.DrawLine(midLeft, topRight, color);
        }

        private static void DrawTwo(float3 position, float scale, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            float3 midLeft = bottomLeft + forward * scale;
            float3 topLeft = bottomLeft + forward * scale * 2;
            
            float3 bottomRight = bottomLeft + right * scale;
            float3 midRight = bottomRight + forward * scale;
            float3 topRight = bottomRight + forward * scale * 2;
            
            Debug.DrawLine(bottomLeft, midLeft, color);
            Debug.DrawLine(midRight, topRight, color);
            
            Debug.DrawLine(midLeft, midRight, color);
            
            Debug.DrawLine(topLeft, topRight, color);
            Debug.DrawLine(bottomLeft, bottomRight, color);
        }

        private static void DrawThree(float3 position, float scale, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            float3 midLeft = bottomLeft + forward * scale;
            float3 topLeft = bottomLeft + forward * scale * 2;
            
            float3 bottomRight = bottomLeft + right * scale;
            float3 midRight = bottomRight + forward * scale;
            float3 topRight = bottomRight + forward * scale * 2;
            
            Debug.DrawLine(bottomRight, midRight, color);
            Debug.DrawLine(midRight, topRight, color);
            
            Debug.DrawLine(midLeft, midRight, color);
            
            Debug.DrawLine(topLeft, topRight, color);
            Debug.DrawLine(bottomLeft, bottomRight, color);
        }
        
        private static void DrawFour(float3 position, float scale, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            float3 midLeft = bottomLeft + forward * scale;
            float3 topLeft = bottomLeft + forward * scale * 2;
            
            float3 bottomRight = bottomLeft + right * scale;
            float3 midRight = bottomRight + forward * scale;
            float3 topRight = bottomRight + forward * scale * 2;
            
            Debug.DrawLine(midLeft, topLeft, color);
            
            Debug.DrawLine(midLeft, midRight, color);
            
            Debug.DrawLine(bottomRight, midRight, color);
            Debug.DrawLine(midRight, topRight, color);
        }

        private static void DrawFive(float3 position, float scale, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            float3 midLeft = bottomLeft + forward * scale;
            float3 topLeft = bottomLeft + forward * scale * 2;
            
            float3 bottomRight = bottomLeft + right * scale;
            float3 midRight = bottomRight + forward * scale;
            float3 topRight = bottomRight + forward * scale * 2;
            
            Debug.DrawLine(midLeft, topLeft, color);
            
            Debug.DrawLine(midLeft, midRight, color);
            
            Debug.DrawLine(bottomRight, midRight, color);
            
            Debug.DrawLine(topLeft, topRight, color);
            Debug.DrawLine(bottomLeft, bottomRight, color);
        }

        private static void DrawSix(float3 position, float scale, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            float3 midLeft = bottomLeft + forward * scale;
            float3 topLeft = bottomLeft + forward * scale * 2;
            
            float3 bottomRight = bottomLeft + right * scale;
            float3 midRight = bottomRight + forward * scale;
            float3 topRight = bottomRight + forward * scale * 2;
            
            Debug.DrawLine(bottomLeft, midLeft, color);
            Debug.DrawLine(midLeft, topLeft, color);
            
            Debug.DrawLine(midLeft, midRight, color);
            
            Debug.DrawLine(bottomRight, midRight, color);
            
            Debug.DrawLine(topLeft, topRight, color);
            Debug.DrawLine(bottomLeft, bottomRight, color);
        }

        private static void DrawSeven(float3 position, float scale, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            float3 midLeft = bottomLeft + forward * scale;
            float3 topLeft = bottomLeft + forward * scale * 2;
            
            float3 bottomRight = bottomLeft + right * scale;
            float3 midRight = bottomRight + forward * scale;
            float3 topRight = bottomRight + forward * scale * 2;
            
            Debug.DrawLine(bottomRight, midRight, color);
            Debug.DrawLine(midRight, topRight, color);
            
            Debug.DrawLine(topLeft, topRight, color);
        }

        private static void DrawEight(float3 position, float scale, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            float3 midLeft = bottomLeft + forward * scale;
            float3 topLeft = bottomLeft + forward * scale * 2;
            
            float3 bottomRight = bottomLeft + right * scale;
            float3 midRight = bottomRight + forward * scale;
            float3 topRight = bottomRight + forward * scale * 2;
            
            Debug.DrawLine(bottomLeft, midLeft, color);
            Debug.DrawLine(midLeft, topLeft, color);
            
            Debug.DrawLine(bottomRight, midRight, color);
            Debug.DrawLine(midRight, topRight, color);
            
            Debug.DrawLine(midLeft, midRight, color);
            
            Debug.DrawLine(topLeft, topRight, color);
            Debug.DrawLine(bottomLeft, bottomRight, color);
        }

        private static void DrawNine(float3 position, float scale, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            float3 midLeft = bottomLeft + forward * scale;
            float3 topLeft = bottomLeft + forward * scale * 2;
            
            float3 bottomRight = bottomLeft + right * scale;
            float3 midRight = bottomRight + forward * scale;
            float3 topRight = bottomRight + forward * scale * 2;
            
            Debug.DrawLine(midLeft, topLeft, color);
            
            Debug.DrawLine(bottomRight, midRight, color);
            Debug.DrawLine(midRight, topRight, color);
            
            Debug.DrawLine(midLeft, midRight, color);
            
            Debug.DrawLine(topLeft, topRight, color);
            Debug.DrawLine(bottomLeft, bottomRight, color);
        }

        public static float3 DrawVector(float3 position, float3 vector, float fontScale, float spacing, Color color)
        {
            float3 currentPosition = position;
            
            currentPosition = DrawLeftBrace(currentPosition, fontScale, spacing, color);
            currentPosition = DrawNumber(currentPosition, vector.x, 1, 2, fontScale, spacing, color);
            currentPosition = DrawComma(currentPosition, fontScale, spacing, color);
            currentPosition = DrawNumber(currentPosition, vector.y, 1, 2, fontScale, spacing, color);
            currentPosition = DrawComma(currentPosition, fontScale, spacing, color);
            currentPosition = DrawNumber(currentPosition, vector.z, 1, 2, fontScale, spacing, color);
            currentPosition = DrawRightBrace(currentPosition, fontScale, spacing, color);
            
            return currentPosition;
        }
        
        public static float3 DrawLeftBrace(float3 position, float fontScale, float spacing, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            float3 midLeft = bottomLeft + forward * fontScale;
            
            float3 bottomRight = bottomLeft + right * fontScale;
            float3 topRight = bottomRight + forward * fontScale * 2;
            
            Debug.DrawLine(midLeft, topRight, color);
            Debug.DrawLine(midLeft, bottomRight, color);
            return position + new float3(1, 0, 0) * (spacing + fontScale);
        }
        
        public static float3 DrawRightBrace(float3 position, float fontScale, float spacing, Color color)
        {
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float3 bottomLeft = position;
            float3 topLeft = bottomLeft + forward * fontScale * 2;
            
            float3 bottomRight = bottomLeft + right * fontScale;
            float3 midRight = bottomRight + forward * fontScale;
            
            Debug.DrawLine(topLeft, midRight, color);
            Debug.DrawLine(bottomLeft, midRight, color);
            return position + new float3(1, 0, 0) * (spacing + fontScale);
        }

        private static readonly float DotSize = 0.25f;
    }
}
#endif