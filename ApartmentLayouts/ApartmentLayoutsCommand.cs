using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ApartmentLayouts
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class ApartmentLayoutsCommand : IExternalCommand
    {
        static List<ElementId> ErrorRoomsList;
        static bool considerAreaCoefficient;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            ErrorRoomsList = new List<ElementId>();

            List<Level> levelsList = new FilteredElementCollector(doc).OfClass(typeof(Level))
                .WhereElementIsNotElementType()
                .OrderBy(lv => lv.get_Parameter(BuiltInParameter.LEVEL_ELEV).AsDouble())
                .Cast<Level>()
                .ToList();
            List<Room> roomList = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement))
                .WhereElementIsNotElementType()
                .Where(r => r.GetType() == typeof(Room))
                .Cast<Room>()
                .ToList();
            List<string> sectionNumberList = GetSectionNumberList(roomList);

            //Проверка наличия общих параметров
            if(roomList.Count != 0)
            {
                //О_НомерСекции - номер секции к которой относится помещение
                Guid sectionNumberParamGuid = new Guid("b59a3474-a5f4-430a-b087-a20f1a4eb57e");
                if(roomList.First().get_Parameter(sectionNumberParamGuid) == null)
                {
                    TaskDialog.Show("Revit", "У помещений отсутствует параметр \"О_НомерСекции\"!");
                    return Result.Cancelled;
                }

                //АР_НомерКвартиры - номер квартиры к которой относится помещение
                Guid apartmentNumberParamGuid = new Guid("10fb72de-237e-4b9c-915b-8849b8907695");
                if (roomList.First().get_Parameter(apartmentNumberParamGuid) == null)
                {
                    TaskDialog.Show("Revit", "У помещений отсутствует параметр \"АР_НомерКвартиры\"!");
                    return Result.Cancelled;
                }

                //АР_ТипПомещения - жилое, нежилое и т.д.
                Guid roomTypeParamGuid = new Guid("7743e986-fcd9-4029-b960-71e522adccab");
                if (roomList.First().get_Parameter(roomTypeParamGuid) == null)
                {
                    TaskDialog.Show("Revit", "У помещений отсутствует параметр \"АР_ТипПомещения\"!");
                    return Result.Cancelled;
                }

                //АР_КоэффПлощади - коэффициент расчета квартирографии
                Guid areaCoefficientParamGuid = new Guid("066eab6d-c348-4093-b0ca-1dfe7e78cb6e");
                if (roomList.First().get_Parameter(areaCoefficientParamGuid) == null)
                {
                    TaskDialog.Show("Revit", "У помещений отсутствует параметр \"АР_КоэффПлощади\"!");
                    return Result.Cancelled;
                }

                //АР_ПлощКвЖилая - сумма площадей жилых комнаят
                Guid apartmentAreaResidentialParamGuid = new Guid("178e222b-903b-48f5-8bfc-b624cd67d13c");
                if (roomList.First().get_Parameter(apartmentAreaResidentialParamGuid) == null)
                {
                    TaskDialog.Show("Revit", "У помещений отсутствует параметр \"АР_ПлощКвЖилая\"!");
                    return Result.Cancelled;
                }

                //АР_ПлощКвартиры - площадь квартиры без 3 и 4
                Guid apartmentAreaParamGuid = new Guid("d3035d0f-b738-4407-a0e5-30787b92fa49");
                if (roomList.First().get_Parameter(apartmentAreaParamGuid) == null)
                {
                    TaskDialog.Show("Revit", "У помещений отсутствует параметр \"АР_ПлощКвартиры\"!");
                    return Result.Cancelled;
                }

                //АР_ПлощКвОбщая - общая площадь квартиры с учетом коэффициентов
                Guid apartmentAreaTotalParamGuid = new Guid("af973552-3d15-48e3-aad8-121fe0dda34e");
                if (roomList.First().get_Parameter(apartmentAreaTotalParamGuid) == null)
                {
                    TaskDialog.Show("Revit", "У помещений отсутствует параметр \"АР_ПлощКвОбщая\"!");
                    return Result.Cancelled;
                }

                //АР_ПлощКвОбщаяБезКоэф - общая площадь квартиры без учета коэффициентов
                Guid apartmentAreaTotalWithoutCoefficientParamGuid = new Guid("f71f6c0b-ed48-4bd9-bf77-bd8c2f8593a7");
                if (roomList.First().get_Parameter(apartmentAreaTotalWithoutCoefficientParamGuid) == null)
                {
                    TaskDialog.Show("Revit", "У помещений отсутствует параметр \"АР_ПлощКвОбщаяБезКоэф\"!");
                    return Result.Cancelled;
                }

                //АР_КолвоКомнат - параметр для вывода кол-ва комнат.
                Guid roomsCountParamGuid = new Guid("a41aaf5b-e9e5-42f0-8a27-6f4bf7e9c9b2");
                if (roomList.First().get_Parameter(roomsCountParamGuid) == null)
                {
                    TaskDialog.Show("Revit", "У помещений отсутствует параметр \"АР_КолвоКомнат\"!");
                    return Result.Cancelled;
                }
            }

            ApartmentLayoutsWPF apartmentLayoutsWPF = new ApartmentLayoutsWPF();
            apartmentLayoutsWPF.ShowDialog();
            if (apartmentLayoutsWPF.DialogResult != true)
            {
                return Result.Cancelled;
            }
            string apartmentLayoutsSettingsSelectionValue = apartmentLayoutsWPF.ApartmentLayoutsSettingsSelectionValue;
            considerAreaCoefficient = apartmentLayoutsWPF.ConsiderAreaCoefficient;

            using (Transaction t = new Transaction(doc))
            {
                t.Start("Квартирография");
                //О_НомерСекции - номер секции к которой относится помещение
                Guid sectionNumberParamGuid = new Guid("b59a3474-a5f4-430a-b087-a20f1a4eb57e");

                if (apartmentLayoutsSettingsSelectionValue == "rbt_SeparatedByLevels")
                {
                    foreach (Level lv in levelsList)
                    {
                        if (sectionNumberList.Count > 1)
                        {
                            foreach (string sn in sectionNumberList)
                            {
                                List<Room> roomListAtLevelAndSection = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement))
                                    .WhereElementIsNotElementType()
                                    .Where(r => r.GetType() == typeof(Room))
                                    .Where(r => r.LevelId == lv.Id)
                                    .Where(r => r.get_Parameter(sectionNumberParamGuid).AsString() == sn)
                                    .Cast<Room>()
                                    .ToList();
                                List<string> apartmentNumberList = GetApartmentNumberList(roomListAtLevelAndSection);

                                SetRoomTypeParam(roomListAtLevelAndSection);
                                if (ErrorRoomsList.Count != 0)
                                {
                                    TaskDialog.Show("Ошибка!!!", $"У {ErrorRoomsList.Count} помещений не заполнен параметр \"АР_ТипПомещения\"!\nЗаполните параметр и перезапустите плагин!");
                                    return Result.Cancelled;
                                }
                                SetApartmentAreas(roomListAtLevelAndSection, apartmentNumberList);
                            }
                        }
                        else
                        {
                            List<Room> roomListAtLevel = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement))
                                    .WhereElementIsNotElementType()
                                    .Where(r => r.GetType() == typeof(Room))
                                    .Where(r => r.LevelId == lv.Id)
                                    .Cast<Room>()
                                    .ToList();
                            List<string> apartmentNumberList = GetApartmentNumberList(roomListAtLevel);

                            SetRoomTypeParam(roomListAtLevel);
                            if (ErrorRoomsList.Count != 0)
                            {
                                TaskDialog.Show("Ошибка!!!", $"У {ErrorRoomsList.Count} помещений не заполнен параметр \"АР_ТипПомещения\"!\nЗаполните параметр и перезапустите плагин!");
                                return Result.Cancelled;
                            }
                            SetApartmentAreas(roomListAtLevel, apartmentNumberList);
                        }
                    }
                }
                else
                {
                    if (sectionNumberList.Count > 1)
                    {
                        foreach (string sn in sectionNumberList)
                        {
                            List<Room> roomListAtSection = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement))
                                .WhereElementIsNotElementType()
                                .Where(r => r.GetType() == typeof(Room))
                                .Where(r => r.get_Parameter(sectionNumberParamGuid).AsString() == sn)
                                .Cast<Room>()
                                .ToList();
                            List<string> apartmentNumberList = GetApartmentNumberList(roomListAtSection);

                            SetRoomTypeParam(roomListAtSection);
                            if (ErrorRoomsList.Count != 0)
                            {
                                TaskDialog.Show("Ошибка!!!", $"У {ErrorRoomsList.Count} помещений не заполнен параметр \"АР_ТипПомещения\"!\nЗаполните параметр и перезапустите плагин!");
                                return Result.Cancelled;
                            }
                            SetApartmentAreas(roomListAtSection, apartmentNumberList);
                        }
                    }
                    else
                    {
                        List<Room> roomListWithout = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement))
                                .WhereElementIsNotElementType()
                                .Where(r => r.GetType() == typeof(Room))
                                .Cast<Room>()
                                .ToList();
                        List<string> apartmentNumberList = GetApartmentNumberList(roomListWithout);

                        SetRoomTypeParam(roomListWithout);
                        if (ErrorRoomsList.Count != 0)
                        {
                            TaskDialog.Show("Ошибка!!!", $"У {ErrorRoomsList.Count} помещений не заполнен параметр \"АР_ТипПомещения\"!\nЗаполните параметр и перезапустите плагин!");
                            return Result.Cancelled;
                        }
                        SetApartmentAreas(roomListWithout, apartmentNumberList);
                    }
                }
                t.Commit();
            }

            TaskDialog.Show("Revit", "Обработка завершена!");
            return Result.Succeeded;
        }

        private static List<string> GetSectionNumberList(List<Room> roomList)
        {
            Guid sectionNumberParamGuid = new Guid("b59a3474-a5f4-430a-b087-a20f1a4eb57e");
            List<string> tempSectionNumberList = new List<string>();
            foreach (Room room in roomList)
            {
                if(room.get_Parameter(sectionNumberParamGuid) != null)
                {
                    string sectionNumber = room.get_Parameter(sectionNumberParamGuid).AsString();
                    if (!tempSectionNumberList.Contains(sectionNumber) && sectionNumber != null)
                    {
                        tempSectionNumberList.Add(sectionNumber);
                    }
                }
            }
            tempSectionNumberList = tempSectionNumberList.OrderBy(n => n).ToList();
            return tempSectionNumberList;
        }
        private static List<string> GetApartmentNumberList(List<Room> roomList)
        {
            Guid apartmentNumberParamGuid = new Guid("10fb72de-237e-4b9c-915b-8849b8907695");
            List<string> tempApartmentNumberList = new List<string>();
            foreach (Room room in roomList)
            {
                string apartmentNumber = room.get_Parameter(apartmentNumberParamGuid).AsString();
                if (!tempApartmentNumberList.Contains(apartmentNumber) && apartmentNumber != null)
                {
                    tempApartmentNumberList.Add(apartmentNumber);
                }
            }
            tempApartmentNumberList = tempApartmentNumberList.OrderBy(n => n).ToList();
            return tempApartmentNumberList;
        }

        private static void SetRoomTypeParam(List<Room> roomList)
        {
            Guid roomTypeParamGuid = new Guid("7743e986-fcd9-4029-b960-71e522adccab");
            Guid areaCoefficientParamGuid = new Guid("066eab6d-c348-4093-b0ca-1dfe7e78cb6e");
            foreach (Room room in roomList)
            {
                double roomTypeParamAsDouble = room.get_Parameter(roomTypeParamGuid).AsDouble();
                if (roomTypeParamAsDouble == 1 || roomTypeParamAsDouble == 2 || roomTypeParamAsDouble == 5)
                {
                    room.get_Parameter(areaCoefficientParamGuid).Set(1);
                }
                else if (roomTypeParamAsDouble == 3)
                {
                    if (considerAreaCoefficient)
                    {
                        room.get_Parameter(areaCoefficientParamGuid).Set(0.5);
                    }
                    else
                    {
                        room.get_Parameter(areaCoefficientParamGuid).Set(1);
                    }
                }
                else if (roomTypeParamAsDouble == 4)
                {
                    if (considerAreaCoefficient)
                    {
                        room.get_Parameter(areaCoefficientParamGuid).Set(0.3);
                    }
                    else
                    {
                        room.get_Parameter(areaCoefficientParamGuid).Set(1);
                    }
                }
                else
                {
                    ErrorRoomsList.Add(room.Id);
                }
            }
        }
        private static void SetApartmentAreas(List<Room> roomList, List<string> apartmentNumberList)
        {
            Guid apartmentNumberParamGuid = new Guid("10fb72de-237e-4b9c-915b-8849b8907695");
            foreach (string apartmentNumber in apartmentNumberList)
            {
                List<Room> apartmentRoomList = new List<Room>();
                foreach (Room room in roomList)
                {
                    if (apartmentNumber == room.get_Parameter(apartmentNumberParamGuid).AsString())
                    {
                        apartmentRoomList.Add(room);
                    }
                }

                //АР_ПлощКвЖилая - сумма площадей жилых комнаят
                Guid apartmentAreaResidentialParamGuid = new Guid("178e222b-903b-48f5-8bfc-b624cd67d13c");
                //АР_ПлощКвартиры - площадь квартиры без 3 и 4
                Guid apartmentAreaParamGuid = new Guid("d3035d0f-b738-4407-a0e5-30787b92fa49");
                //АР_ПлощКвОбщая - общая площадь квартиры с учетом коэффициентов
                Guid apartmentAreaTotalParamGuid = new Guid("af973552-3d15-48e3-aad8-121fe0dda34e");
                //АР_ПлощКвОбщаяБезКоэф - общая площадь квартиры без учета коэффициентов
                Guid apartmentAreaTotalWithoutCoefficientParamGuid = new Guid("f71f6c0b-ed48-4bd9-bf77-bd8c2f8593a7");
                //АР_КолвоКомнат - параметр для вывода кол-ва комнат.
                Guid roomsCountParamGuid = new Guid("a41aaf5b-e9e5-42f0-8a27-6f4bf7e9c9b2");

                double apartmentAreaResidential = 0;
                double apartmentArea = 0;
                double apartmentAreaTotal = 0;
                double apartmentAreaTotalWithoutCoefficient = 0;
                double roomsCount = 0;

                double residentialRoomsArea = 0;
                double nonResidentialRoomsArea = 0;
                double loggiasArea = 0;
                double loggiasAreaWithCoefficient = 0;
                double balconiesArea = 0;
                double balconiesAreaWithCoefficient = 0;

                foreach (Room room in apartmentRoomList)
                {
                    if (room.Area == 0)
                    {
                        continue;
                    }
                    Guid roomTypeParamGuid = new Guid("7743e986-fcd9-4029-b960-71e522adccab");
                    double roomTypeParamAsDouble = room.get_Parameter(roomTypeParamGuid).AsDouble();

                    if (roomTypeParamAsDouble == 1)
                    {
#if R2019 || R2020 || R2021
                        double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), DisplayUnitType.DUT_SQUARE_METERS) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                        residentialRoomsArea += area;
#else
                        double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), UnitTypeId.SquareMeters) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                        residentialRoomsArea += area;
#endif
                        roomsCount += 1;
                    }
                    else if (roomTypeParamAsDouble == 2) 
                    {
#if R2019 || R2020 || R2021
                        double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), DisplayUnitType.DUT_SQUARE_METERS) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                        nonResidentialRoomsArea += area;
#else
                        double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), UnitTypeId.SquareMeters) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                        nonResidentialRoomsArea += area;
#endif
                    }
                    else if (roomTypeParamAsDouble == 3)
                    {
                        if (considerAreaCoefficient)
                        {
#if R2019 || R2020 || R2021
                            double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), DisplayUnitType.DUT_SQUARE_METERS) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                            loggiasArea += area;
                            loggiasAreaWithCoefficient += Math.Round(area * 0.5, 2, MidpointRounding.AwayFromZero);
#else
                            double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), UnitTypeId.SquareMeters) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                            loggiasArea += area;
                            loggiasAreaWithCoefficient += Math.Round(area * 0.5, 2, MidpointRounding.AwayFromZero);
#endif
                        }
                        else
                        {
#if R2019 || R2020 || R2021
                            double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), DisplayUnitType.DUT_SQUARE_METERS) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                            loggiasArea += area;
                            loggiasAreaWithCoefficient += Math.Round(area, 2, MidpointRounding.AwayFromZero);
#else
                            double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), UnitTypeId.SquareMeters) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                            loggiasArea += area;
                            loggiasAreaWithCoefficient += Math.Round(area, 2, MidpointRounding.AwayFromZero);
#endif
                        }

                    }
                    else if (roomTypeParamAsDouble == 4)
                    {
                        if (considerAreaCoefficient)
                        {
#if R2019 || R2020 || R2021
                            double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), DisplayUnitType.DUT_SQUARE_METERS) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                            balconiesArea += area;
                            balconiesAreaWithCoefficient += Math.Round(area * 0.3, 2, MidpointRounding.AwayFromZero);
#else
                            double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), UnitTypeId.SquareMeters) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                            balconiesArea += area;
                            balconiesAreaWithCoefficient += Math.Round(area * 0.3, 2, MidpointRounding.AwayFromZero);
#endif
                        }
                        else
                        {
#if R2019 || R2020 || R2021
                            double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), DisplayUnitType.DUT_SQUARE_METERS) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                            balconiesArea += area;
                            balconiesAreaWithCoefficient += Math.Round(area, 2, MidpointRounding.AwayFromZero);
#else
                            double area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble(), UnitTypeId.SquareMeters) + 0.00000000005, 2, MidpointRounding.AwayFromZero);
                            balconiesArea += area;
                            balconiesAreaWithCoefficient += Math.Round(area, 2, MidpointRounding.AwayFromZero);
#endif
                        }
                    }
                }

                residentialRoomsArea = Math.Round(residentialRoomsArea, 2, MidpointRounding.AwayFromZero);
                nonResidentialRoomsArea = Math.Round(nonResidentialRoomsArea, 2, MidpointRounding.AwayFromZero);
                loggiasArea = Math.Round(loggiasArea, 2, MidpointRounding.AwayFromZero);
                loggiasAreaWithCoefficient = Math.Round(loggiasAreaWithCoefficient, 2, MidpointRounding.AwayFromZero);
                balconiesArea = Math.Round(balconiesArea, 2, MidpointRounding.AwayFromZero);
                balconiesAreaWithCoefficient = Math.Round(balconiesAreaWithCoefficient, 2, MidpointRounding.AwayFromZero);

#if R2019 || R2020 || R2021
                apartmentAreaResidential = UnitUtils.ConvertToInternalUnits(residentialRoomsArea, DisplayUnitType.DUT_SQUARE_METERS);
                apartmentArea = UnitUtils.ConvertToInternalUnits(residentialRoomsArea + nonResidentialRoomsArea, DisplayUnitType.DUT_SQUARE_METERS);
                apartmentAreaTotal = UnitUtils.ConvertToInternalUnits(residentialRoomsArea + nonResidentialRoomsArea + loggiasAreaWithCoefficient + balconiesAreaWithCoefficient, DisplayUnitType.DUT_SQUARE_METERS);
                apartmentAreaTotalWithoutCoefficient = UnitUtils.ConvertToInternalUnits(residentialRoomsArea + nonResidentialRoomsArea + loggiasArea + balconiesArea, DisplayUnitType.DUT_SQUARE_METERS);
#else
                apartmentAreaResidential = UnitUtils.ConvertToInternalUnits(residentialRoomsArea, UnitTypeId.SquareMeters);
                apartmentArea =  UnitUtils.ConvertToInternalUnits(residentialRoomsArea + nonResidentialRoomsArea, UnitTypeId.SquareMeters);
                apartmentAreaTotal = UnitUtils.ConvertToInternalUnits(residentialRoomsArea + nonResidentialRoomsArea + loggiasAreaWithCoefficient + balconiesAreaWithCoefficient, UnitTypeId.SquareMeters);
                apartmentAreaTotalWithoutCoefficient = UnitUtils.ConvertToInternalUnits(residentialRoomsArea + nonResidentialRoomsArea + loggiasArea + balconiesArea, UnitTypeId.SquareMeters);
#endif

                foreach (Room room in apartmentRoomList)
                {
                    room.get_Parameter(apartmentAreaResidentialParamGuid).Set(apartmentAreaResidential);
                    room.get_Parameter(apartmentAreaParamGuid).Set(apartmentArea);
                    room.get_Parameter(apartmentAreaTotalParamGuid).Set(apartmentAreaTotal);
                    room.get_Parameter(apartmentAreaTotalWithoutCoefficientParamGuid).Set(apartmentAreaTotalWithoutCoefficient);
                    room.get_Parameter(roomsCountParamGuid).Set(roomsCount);
                }
            }
        }
    }
}
