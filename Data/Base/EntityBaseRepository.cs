//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Linq.Expressions;
//using System.Threading.Tasks;
//using Firebase.Database;
//using Firebase.Database.Query;
//using GraduationProject.Models;

//namespace GraduationProjectFirebaseVersion.Data.Base
//{
//    public class EntityBaseRepository<T> : IEntityBaseRepository<T> where T : class, IEntityBase, new()
//    {
//        private readonly FirebaseClient _client;

//        public EntityBaseRepository(FirebaseClient client)
//        {
//            _client = client;
//        }

//        public async Task AddAsync(T entity)
//        {
//            var response = await _client.Child(typeof(T).Name).PostAsync(entity);
//            // Get the generated key and set it as the entity's ID
//            var key = response.Key;
//            entity.Id = Convert.ToInt32(response.Key);
//        }

//        public async Task DeleteAsync(int id)
//        {
//            await _client.Child(typeof(T).Name).Child(id.ToString()).DeleteAsync();
//        }

//        public async Task<IEnumerable<T>> GetAllAsync()
//        {
//            var response = await _client.Child(typeof(T).Name).OnceAsync<T>();
//            return response.Select(x => x.Object);
//        }

//        public async Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includeProperties)
//        {
//            throw new NotImplementedException("Include properties is not supported in Firebase Realtime Database.");
//        }

//        public async Task<T> GetByIdAsync(int id)
//        {
//            var response = await _client.Child(typeof(T).Name).Child(id.ToString()).OnceSingleAsync<T>();
//            return response;
//        }

//        public async Task UpdateAsync(int id, T entity)
//        {
//            await _client.Child(typeof(T).Name).Child(id.ToString()).PutAsync(entity);
//        }

//        public async Task<Dictionary<int, List<BookedDates>>> GetBookedDatesForProjectsAsync(List<int> projectIds)
//        {
//            throw new NotImplementedException("This method is not supported in Firebase Realtime Database.");
//        }
//    }
//}
