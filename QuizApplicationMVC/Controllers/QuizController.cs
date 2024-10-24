using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuizApplicationMVC.Data;
using QuizApplicationMVC.Models;

namespace QuizApplicationMVC.Controllers
{
    public class QuizController : Controller
    {
        private readonly ApplicationDBContext _context;

        public QuizController(ApplicationDBContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetInt32("Id") == null)
            {
                return RedirectToAction("Login", "Users");
            }

            if (TempData.ContainsKey("QuizEvaluated") && (bool)TempData["QuizEvaluated"])
            {
                TempData.Remove("QuizEvaluated"); 
            }

            var quizzes = await _context.Quiz.Include(q => q.Questions).ToListAsync();

            var quizzesWithQuestions = quizzes.Where(q => q.Questions.Any()).ToList();

            var quizzesToRemove = quizzes.Except(quizzesWithQuestions).ToList();

            if (quizzesToRemove.Any())
            {
                _context.Quiz.RemoveRange(quizzesToRemove);
                await _context.SaveChangesAsync();
            }

            return View(quizzesWithQuestions);
        }


        public async Task<IActionResult> MyQuizes()
        {
            
            if (HttpContext.Session.GetInt32("Id") == null)
            {
                return RedirectToAction("Login", "Users");
            }

            
            int userId = HttpContext.Session.GetInt32("Id") ?? 0;

            
            if (TempData.ContainsKey("QuizEvaluated") && (bool)TempData["QuizEvaluated"])
            {
                TempData.Remove("QuizEvaluated"); 
            }

            var quizzes = await _context.Quiz
                .Where(q => q.UserId == userId) 
                .ToListAsync();
            //foreach (var quiz in quizzes)
            //{
                
            //Console.WriteLine(quiz.UserId);
            //}


            if (quizzes != null)
            {
                return View(quizzes);
            }
            else
            {
                return Problem("Entity set 'ApplicationDBContext.Quiz' is null.");
            }
        }



        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Quiz == null)
            {
                return NotFound();
            }

            var quiz = await _context.Quiz
                        .Include(q => q.User)
                        .FirstOrDefaultAsync(m => m.Id == id);
            Console.WriteLine("quiz.User");
            Console.WriteLine(quiz.User);
            if (quiz == null)
            {
                return NotFound();
            }

            return View(quiz);
        }

        [HttpPost, ActionName("Evaluate")]
        [ValidateAntiForgeryToken]
        public IActionResult Evaluate(List<QuizQuestion> quizQuestions)
        {
            if (HttpContext.Session.GetInt32("Id") == null)
            {
                return RedirectToAction("Login", "Users");
            }
            if (TempData.ContainsKey("QuizEvaluated") && (bool)TempData["QuizEvaluated"])
            {
                return RedirectToAction("Index", "Home"); 


            }

            if (quizQuestions == null || !quizQuestions.Any())
            {
                return NotFound();
            }
            
            if (ModelState.IsValid)
            {
                int quizId = -1;
                int correctAnswers = 0;
                foreach (var newQuestion in quizQuestions)
                {

                    var originalQuestion = _context.QuizQuestion.FirstOrDefault(q => q.Id == newQuestion.Id);

                    if (originalQuestion != null)
                    {
                        originalQuestion.SelectedOption = newQuestion.SelectedOption;
                        _context.SaveChanges();

                        if (newQuestion.SelectedOption == originalQuestion.CorrectOption)
                        {
                            correctAnswers++;
                        }
                        quizId = originalQuestion.QuizId;
                    }
                    else
                    {
                        Console.WriteLine("not found asjb");
                    }
                }

                TempData["QuizEvaluated"] = true; 

                TempData["CorrectAnswersCount"] = correctAnswers;

                int userId = (int)HttpContext.Session.GetInt32("Id");
                var user = _context.Users.FirstOrDefault(u => u.Id == userId);
                
                var quiz = _context.Quiz.FirstOrDefault(q => q.Id == quizId);

                var quizUserHistory = new QuizUserHistory
                {
                    UserId = userId,
                    QuizId = quizId,
                    Score = correctAnswers,
                    DateTaken = DateTime.Now,
                    User = user,
                    Quiz = quiz
                };
                
                _context.QuizUserHistory.Add(quizUserHistory);
                _context.SaveChanges();


                return View("EvaluationResult", quizQuestions);
            }
            else
            {
                foreach (var key in ModelState.Keys)
                {
                    var modelStateEntry = ModelState[key];
                    foreach (var error in modelStateEntry.Errors)
                    {
                        var errorMessage = error.ErrorMessage;
                        Console.WriteLine($"Validation Error for {key}: {errorMessage}");
                    }
                }
                return View("Take", quizQuestions);
            }
        }


        public IActionResult EvaluationResult()
        {
            return View();
        }

        public async Task<IActionResult> Take(int? id)
        {
            if (id == null || _context.Quiz == null)
            {
                return NotFound();
            }

            if (TempData.ContainsKey("QuizEvaluated") && (bool)TempData["QuizEvaluated"])
            {
                TempData.Remove("QuizEvaluated"); 

                return RedirectToAction("Index", "Home"); 
            }

            var questions = await _context.Questions
                .Where(q => q.QuizId == id)
                .ToListAsync();

            if (questions == null)
            {
                return NotFound();
            }

            var quizQuestions = questions.Select(q => new QuizQuestion
            {
                QuestionName = q.QuestionsName,
                OptionA = q.OptionA,
                OptionB = q.OptionB,
                OptionC = q.OptionC,
                OptionD = q.OptionD,
                SelectedOption = "",
                CorrectOption = q.CorrectOption,
                QuizId = (int)id
            }).ToList();

            foreach (var question in quizQuestions)
            {
                _context.Add(question);
            }

            await _context.SaveChangesAsync();
            return View(quizQuestions);
        }



        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Description")] Quiz quiz)
        {
            if (HttpContext.Session.GetInt32("Id") == null)
            {
                return RedirectToAction("Login", "Users");
            }

            var userId = (int)HttpContext.Session.GetInt32("Id");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            quiz.User = user;
            
            quiz.Questions = null;
            if (ModelState.IsValid)
            {
                _context.Add(quiz);
                await _context.SaveChangesAsync();
                HttpContext.Session.SetInt32("QuizId", quiz.Id);


                return RedirectToAction("Create", "Questions", null);
            }
            else
            {
                Console.WriteLine("Validation Errors:");
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        Console.WriteLine(error.ErrorMessage);
                    }
                }
            }


            return View(quiz);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Quiz == null)
            {
                return NotFound();
            }

            var quiz = await _context.Quiz.FindAsync(id);
            if (quiz == null)
            {
                return NotFound();
            }
            return View(quiz);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description")] Quiz quiz)
        {
            if (id != quiz.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingQuiz = await _context.Quiz.FindAsync(quiz.Id);
                    if (existingQuiz == null)
                    {
                        return NotFound();
                    }

                    existingQuiz.Title = quiz.Title;
                    existingQuiz.Description = quiz.Description;

                    _context.Update(existingQuiz);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(MyQuizes));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!QuizExists(quiz.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }


            return View(quiz);
        }



        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Quiz == null)
            {
                return NotFound();
            }

            var quiz = await _context.Quiz
                        .Include(q => q.User)
                        .FirstOrDefaultAsync(m => m.Id == id);
            if (quiz == null)
            {
                return NotFound();
            }

            return View(quiz);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Quiz == null)
            {
                return Problem("Entity set 'ApplicationDBContext.Quiz'  is null.");
            }
            var quiz = await _context.Quiz.FindAsync(id);
            if (quiz != null)
            {
                var relatedHistoryRecords = _context.QuizUserHistory.Where(qu => qu.QuizId == id);
                _context.QuizUserHistory.RemoveRange(relatedHistoryRecords);

                _context.Quiz.Remove(quiz);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(MyQuizes));
        }

        private bool QuizExists(int id)
        {
          return (_context.Quiz?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
