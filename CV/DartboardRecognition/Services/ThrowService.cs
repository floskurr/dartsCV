﻿#region Usings

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using DartboardRecognition.Windows;
using Emgu.CV.Structure;
using NLog;
using System.Text.Json;

#endregion

namespace DartboardRecognition.Services
{
    public class ThrowService
    {
        private readonly DrawService drawService;
        private readonly Logger logger;
        private readonly List<Ray> rays;
        private readonly Queue<Throw> throwsCollection;

        public ThrowService(DrawService drawService, Logger logger)
        {
            this.logger = logger;
            this.drawService = drawService;
            rays = new List<Ray>();
            throwsCollection = new Queue<Throw>();
        }

        public void CalculateAndSaveThrow()
        {
            logger.Debug($"Calculate throw start");

            if (rays.Count < 2)
            {
                logger.Debug($"Rays count < 2. Calculate throw end.");

                rays.Clear();
                return;
            }

            foreach (var ray in rays)
            {
                logger.Info($"Ray:'{ray}'");
            }

            var firstBestRay = rays.OrderByDescending(i => i.ContourArc).ElementAt(0);
            var secondBestRay = rays.OrderByDescending(i => i.ContourArc).ElementAt(1);
            rays.Clear();

            logger.Info($"Best rays:'{firstBestRay}' and '{secondBestRay}'");

            var poi = MeasureService.FindLinesIntersection(firstBestRay.CamPoint,
                                                           firstBestRay.RayPoint,
                                                           secondBestRay.CamPoint,
                                                           secondBestRay.RayPoint);
            var anotherThrow = PrepareThrowData(poi);
            throwsCollection.Enqueue(anotherThrow);

            drawService.ProjectionDrawLine(firstBestRay.CamPoint, firstBestRay.RayPoint, new Bgr(Color.Aqua).MCvScalar, true);
            drawService.ProjectionDrawLine(secondBestRay.CamPoint, secondBestRay.RayPoint, new Bgr(Color.Aqua).MCvScalar, false);
            drawService.ProjectionDrawThrow(poi, false);
            drawService.PrintThrow(anotherThrow);
            Console.WriteLine("Total:" + anotherThrow.TotalPoints);
            Console.WriteLine("Multi:" + anotherThrow.Multiplier);
            Console.WriteLine("Sektor:" + anotherThrow.Sector);

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(variables.apiConnectionString);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            object[] formatArgs = new object[] { anotherThrow.Sector, anotherThrow.Multiplier, anotherThrow.TotalPoints };
            string throwJson = "{\"Sector\": " + anotherThrow.Sector + ",\"Multiplier\": " + anotherThrow.Multiplier + ",\"TotalPoints\": " + anotherThrow.TotalPoints + "}"; // not pretty but works (fuck string.format)

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(throwJson);
            }
            
            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                Console.WriteLine(result);
            }

            logger.Info($"Throw:{anotherThrow}");
            logger.Debug($"Calculate throw end.");
        }

        private Throw PrepareThrowData(PointF poi)
        {
            var sectors = new List<int>()
                          {
                              14, 9, 12, 5, 20,
                              1, 18, 4, 13, 6,
                              10, 15, 2, 17, 3,
                              19, 7, 16, 8, 11
                          };
            var angle = MeasureService.FindAngle(drawService.projectionCenterPoint, poi);
            var distance = MeasureService.FindDistance(drawService.projectionCenterPoint, poi);
            var sector = 0;
            var type = ThrowType.Single;

            if (distance >= drawService.projectionCoefficent * 95 &&
                distance <= drawService.projectionCoefficent * 105)
            {
                type = ThrowType.Tremble;
            }
            else if (distance >= drawService.projectionCoefficent * 160 &&
                     distance <= drawService.projectionCoefficent * 170)
            {
                type = ThrowType.Double;
            }

            // Find sector
            if (distance <= drawService.projectionCoefficent * 7)
            {
                sector = 50;
                type = ThrowType.Bull;
            }
            else if (distance > drawService.projectionCoefficent * 7 &&
                     distance <= drawService.projectionCoefficent * 17)
            {
                sector = 25;
                type = ThrowType._25;
            }
            else if (distance > drawService.projectionCoefficent * 170)
            {
                sector = 0;
                type = ThrowType.Zero;
            }
            else
            {
                var startRadSector = -2.9845105;
                var radSectorStep = 0.314159;
                var radSector = startRadSector;
                foreach (var proceedSector in sectors)
                {
                    if (angle >= radSector && angle < radSector + radSectorStep)
                    {
                        sector = proceedSector;
                        break;
                    }

                    sector = 11; // todo - works, but not looks pretty

                    radSector += radSectorStep;
                }
            }

            return new Throw(poi, sector, type, drawService.projectionFrameSide);
        }

        public void SaveRay(Ray ray)
        {
            rays.Add(ray);
        }
    }
}