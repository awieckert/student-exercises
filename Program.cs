using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using nss.Data;
using Dapper;
using System.Text;

/*
    To install required packages from NuGet
        1. `dotnet add package Microsoft.Data.Sqlite`
        2. `dotnet add package Dapper`
        3. `dotnet restore`
 */

namespace nss
{
    class Program
    {
        static void Main(string[] args)
        {
            SqliteConnection db = DatabaseInterface.Connection;
            DatabaseInterface.CheckCohortTable();
            DatabaseInterface.CheckInstructorsTable();
            DatabaseInterface.CheckExerciseTable();
            DatabaseInterface.CheckStudentTable();
            // StudentExercise.Create(db);
            // StudentExercise.Seed(db);


            List<Instructor> instructors = db.Query<Instructor>(@"SELECT * FROM Instructor").ToList();
            instructors.ForEach(i => Console.WriteLine($"{i.FirstName} {i.LastName}"));

            List<Exercise> exercises = db.Query<Exercise>(@"SELECT * FROM Exercise").ToList();
            exercises.ForEach(e => Console.WriteLine($"{e.Name}"));

            List<Student> students = db.Query<Student>(@"SELECT * FROM Student").ToList();
            students.ForEach(s => Console.WriteLine($"{s.FirstName} {s.LastName}"));

            db.Query<Cohort>(@"SELECT * FROM Cohort")
              .ToList()
              .ForEach(i => Console.WriteLine($"{i.Name}"));




            /*
                Query the database for each instructor, and join in the instructor's cohort.
                Since an instructor is only assigned to one cohort at a time, you can simply
                assign the corresponding cohort as a property on the instance of the
                Instructor class that is created by Dapper.
             */
            db.Query<Instructor, Cohort, Instructor>(@"
                SELECT i.CohortId,
                       i.FirstName,
                       i.LastName,
                       i.SlackHandle,
                       i.Specialty,
                       i.Id,
                       c.Id,
                       c.Name
                FROM Instructor i
                JOIN Cohort c ON c.Id = i.CohortId
            ", (instructor, cohort) =>
            {
                instructor.Cohort = cohort;
                return instructor;
            })
            .ToList()
            .ForEach(i => Console.WriteLine($"{i.FirstName} {i.LastName} ({i.SlackHandle}) is coaching {i.Cohort.Name}"));




            /*
                Querying the database in the opposite direction is noticeably more
                complex and abstract. In the query below, you start with the Cohort
                table, and join the Instructor table. Since more than one instructor
                can be assigned to a Cohort, then you get multiple rows in the result.

                Example:
                    1,"Evening Cohort 1",1,"Steve","Brownlee",1,"@coach","Dad jokes"
                    5,"Day Cohort 13",2,"Joe","Shepherd",5,"@joes","Analogies"
                    6,"Day Cohort 21",3,"Jisie","David",6,"@jisie","Student success"
                    6,"Day Cohort 21",4,"Emily","Lemmon",6,"@emlem","Latin"

                If you want to consolidate both Jisie and Emily into a single
                collection of Instructors assigned to Cohort 21, you will need to
                create a Dictionary and build it up yourself from the result set.

                - The unique keys in the Dictionary will be Id of each Cohort
                - The value will be an instance of the Cohort class, which has an
                        Instructors property.
             */
            Dictionary<int, Cohort> report = new Dictionary<int, Cohort>();

            db.Query<Cohort, Instructor, Cohort>(@"
                SELECT
                       c.Id,
                       c.Name,
                       i.Id,
                       i.FirstName,
                       i.LastName,
                       i.CohortId,
                       i.SlackHandle,
                       i.Specialty
                FROM Cohort c
                JOIN Instructor i ON c.Id = i.CohortId
            ", (cohort, instructor) =>
            {
                // Does the Dictionary already have the key of the cohort Id?
                if (!report.ContainsKey(cohort.Id))
                {
                    // Create the entry in the dictionary
                    report[cohort.Id] = cohort;
                }

                // Add the instructor to the current cohort entry in Dictionary
                report[cohort.Id].Instructors.Add(instructor);
                return cohort;
            });

            /*
                Iterate the key/value pairs in the dictionary
             */
            foreach (KeyValuePair<int, Cohort> cohort in report)
            {
                Console.WriteLine($"{cohort.Value.Name} has {cohort.Value.Instructors.Count} instructors.");
            }




            /*
                Navigating a Many To Many relationship in the database is largely
                the same process. The SQL will definitely change since you need
                to join the two resources through the intersection table.
             */
            Dictionary<int, Student> studentExercises = new Dictionary<int, Student>();

            db.Query<Student, Exercise, Student>(@"
                SELECT
                       s.Id,
                       s.FirstName,
                       s.LastName,
                       s.SlackHandle,
                       e.Id,
                       e.Name,
                       e.Language
                FROM Student s
                JOIN StudentExercise se ON s.Id = se.StudentId
                JOIN Exercise e ON se.ExerciseId = e.Id
            ", (student, exercise) =>
            {
                if (!studentExercises.ContainsKey(student.Id))
                {
                    studentExercises[student.Id] = student;
                }
                studentExercises[student.Id].AssignedExercises.Add(exercise);
                return student;
            });

            foreach (KeyValuePair<int, Student> student in studentExercises)
            {
                List<string> assignedExercises = new List<string>();
                student.Value.AssignedExercises.ForEach(e => assignedExercises.Add(e.Name));

                Console.WriteLine($@"{student.Value.FirstName} {student.Value.LastName} is working on {String.Join(',', assignedExercises)}.");
            }




            /*
                If you need to join additional tables, just add the corresponding
                model to the list of types for Query method. In the example below,
                you have augmented the query above by including a JOIN to the
                Cohort table. Therefore, the Query method must be typed as
                <Student, Exercise, Cohort, Student>.
             */
            Dictionary<int, Student> verboseStudents = new Dictionary<int, Student>();

            db.Query<Student, Exercise, Cohort, Student>(@"
                SELECT
                       s.Id,
                       s.FirstName,
                       s.LastName,
                       s.SlackHandle,
                       e.Id,
                       e.Name,
                       e.Language,
                       c.Id,
                       c.Name
                FROM Student s
                JOIN StudentExercise se ON s.Id = se.StudentId
                JOIN Exercise e ON se.ExerciseId = e.Id
                JOIN Cohort c ON s.CohortId = c.Id
            ", (student, exercise, cohort) =>
            {
                if (!verboseStudents.ContainsKey(student.Id))
                {
                    verboseStudents[student.Id] = student;
                }
                verboseStudents[student.Id].AssignedExercises.Add(exercise);
                verboseStudents[student.Id].Cohort = cohort;
                return student;
            });

            /*
                Display the student information using the StringBuilder class
             */
            foreach (KeyValuePair<int, Student> student in verboseStudents)
            {
                List<string> assignedExercises = new List<string>();
                student.Value.AssignedExercises.ForEach(e => assignedExercises.Add(e.Name));

                StringBuilder output = new StringBuilder(100);
                output.Append($"{student.Value.FirstName} {student.Value.LastName} ");
                output.Append($"in {student.Value.Cohort.Name} ");
                output.Append($"is working on {String.Join(',', assignedExercises)}.");
                Console.WriteLine(output);
            }

            Dictionary<int, Cohort> allTheCohorts = new Dictionary<int, Cohort>();

            db.Query<Cohort, Student, Instructor, Cohort>(@"
                SELECT  c.Id,
                        c.Name,
                        s.Id,
                        s.FirstName,
                        s.LastName,
                        s.SlackHandle,
                        i.Id,
                        i.FirstName,
                        i.LastName,
                        i.SlackHandle,
                        i.CohortId
                FROM Cohort c
                JOIN Student s on c.Id = s.CohortId
                LEFT JOIN Instructor i on c.Id = i.CohortId
            ", (cohort, student, instructor) => {
                if (!allTheCohorts.ContainsKey(cohort.Id)){
                  allTheCohorts[cohort.Id] = cohort;
                }
                if (student != null) {
                  allTheCohorts[cohort.Id].Students.Add(student);
                }
                if (instructor != null) {
                  allTheCohorts[cohort.Id].Instructors.Add(instructor);
                }
                return cohort;
            });

            foreach (KeyValuePair<int, Cohort> cohort in allTheCohorts)
            {
                // int instructorCounter = 0;
                // int studentCounter = 0;
                // cohort.Value.Students.ForEach(item => { if(item != null) studentCounter++; });
                // cohort.Value.Instructors.ForEach(item => { if(item != null) instructorCounter++; });
                Console.WriteLine($@"{cohort.Value.Name} has {cohort.Value.Students.Count} student and {cohort.Value.Instructors.Count} instructors");
            }

            Dictionary<int, Exercise> allExercises = new Dictionary<int, Exercise>();

            db.Query<Exercise, Student, Instructor, StudentExercise, StudentExercise>(@"
            SELECT  e.Id,
                e.Name,
                e.Language,
                s.Id,
                s.FirstName,
                s.LastName,
                s.SlackHandle,
                s.CohortId,
                i.Id,
                i.FirstName,
                i.LastName,
                i.SlackHandle,
                i.Specialty,
                i.CohortId,
                se.Id,
                se.ExerciseId,
                se.StudentId,
                se.InstructorId
              FROM Exercise e
              JOIN StudentExercise se on e.Id = se.ExerciseId
              JOIN Student s on se.StudentId = s.Id
              JOIN Instructor i on se.InstructorId = i.Id
            ", (exerc, student, instruc, studentExercise) => {
              studentExercise.Instructor = instruc;
              studentExercise.Student = student;
              studentExercise.Exercise = exerc;

              if (!allExercises.ContainsKey(exerc.Id)) {
                exerc.AssignedInfo.Add(studentExercise);
                allExercises[exerc.Id] = exerc;
              } else {
                allExercises[exerc.Id].AssignedInfo.Add(studentExercise);
              }
              return studentExercise;
            });

            foreach (KeyValuePair<int, Exercise> item in allExercises)
            {
                Console.WriteLine($"Students assigned {item.Value.Name}: ");

                foreach (StudentExercise exercise in item.Value.AssignedInfo)
                {
                    Console.WriteLine($"   {exercise.Student.FirstName} {exercise.Student.LastName} assigned by {exercise.Instructor.FirstName}");
                }
            }

            Dictionary<int, Cohort> allCohorts = new Dictionary<int, Cohort>();

            db.Query<Student, Instructor, Cohort, StudentExercise, Exercise, Cohort>(@"
                    SELECT  s.Id,
                            s.FirstName,
                            s.LastName,
                            s.SlackHandle,
                            s.CohortId,
                            i.Id,
                            i.FirstName,
                            i.LastName,
                            i.SlackHandle,
                            i.Specialty,
                            i.CohortId,
                            c.Id,
                            c.Name,
                            se.Id,
                            se.ExerciseId,
                            se.StudentId,
                            se.InstructorId,
						                e.Id,
						                e.Name,
                            e.Language
                    FROM Cohort c
                    JOIN Student s on c.Id = s.CohortId
                    LEFT JOIN Instructor i on c.Id = i.CohortId
					          JOIN StudentExercise se on s.Id = se.StudentId
					          JOIN Exercise e on se.ExerciseId = e.Id
                    ", (student, instruc, cohort, studentExercise, exercise) => {

                        // Determine if cohort Id exists in the dicitonary if not do the following
                        if (!allCohorts.ContainsKey(cohort.Id)) {

                        // Add the exercise to the current student
                          student.AssignedExercises.Add(exercise);

                        // If instructor is not null add it to the cohort
                          if (instruc != null) {
                            cohort.Instructors.Add(instruc);
                          }

                        // Add the student with the newly assigned exercise to the Students on the current cohort and create the Key on the dictionary for the current Cohort and make it's Value = the current cohort
                          cohort.Students.Add(student);
                          allCohorts[cohort.Id] = cohort;
                        } else {

                        // If the cohort ID exists on the dictionary to the following:
                        // student.AssignedExercises.Add(exercise);


                        // If the cohort's Instructors list does not have Any instructors with the current instructors first name, add that instructor to the Instructors list on cohort
                          if (!allCohorts[cohort.Id].Instructors.Any(ins => ins.FirstName == instruc.FirstName)) {
                            allCohorts[cohort.Id].Instructors.Add(instruc);
                          }

                        // Loop over all the students in the Student list on the current Cohort
                          foreach (Student stud in allCohorts[cohort.Id].Students)
                          {
                          // If the student already exists, add the current Exercise to the AssignedExercises list on that student
                              if (stud.FirstName == student.FirstName) {
                                stud.AssignedExercises.Add(exercise);
                              } else {
                          // If the student doesn't exist, add the exercise to the current student and add that new student to the Students list on the current cohort
                                student.AssignedExercises.Add(exercise);
                                allCohorts[cohort.Id].Students.Add(student);
                              }
                          }

                          // allCohorts[cohort.Id].Students.ForEach(stud => { if (stud.FirstName == student.FirstName) stud.AssignedExercises.Add(exercise); });

                        }
                        return cohort;
                    });

                    foreach (KeyValuePair<int, Cohort> cohort in allCohorts)
                    {
                        Console.WriteLine($"-{cohort.Value.Name}");

                        foreach (Student stud in cohort.Value.Students)
                        {
                          List<string> studExercises = new List<string>();
                          foreach (Exercise ex in stud.AssignedExercises)
                          {
                            studExercises.Add(ex.Name);
                          }
                          Console.WriteLine($"   {stud.SlackHandle} is a student and is assigned {String.Join(" & ", studExercises)}");
                        }

                        if (cohort.Value.Instructors.Count != 0) {
                          foreach (Instructor inst in cohort.Value.Instructors)
                          {
                            Console.WriteLine($"   {inst.FirstName} {inst.LastName} is an instructor");
                          }
                        }


                    }

            // For each cohort, list the students and instructors
            /*
                1. Create Exercises table and seed it
                2. Create Student table and seed it  (use sub-selects)
                3. Create StudentExercise table and seed it (use sub-selects)
                4. List the instructors and students assigned to each cohort
                5. List the students working on each exercise, include the
                   student's cohort and the instructor who assigned the exercise
             */
        }
    }
}
